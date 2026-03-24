using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Activer.Core.Engine;
using Activer.Core.Services;
using Activer.Services;
using Activer.ViewModels;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Activer;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly Win32IdleService idleService;
    private readonly LogWindow logWindow;
    private readonly Forms.ContextMenuStrip trayMenu;
    private readonly Icon trayIconResource;
    private readonly Forms.NotifyIcon trayIcon;
    private bool isHiddenToTray;
    private VersionWindow? currentVersionWindow;

    public MainWindow()
    {
        InitializeComponent();

        var clock = new SystemClock();
        var randomSource = new RandomSource();
        var engine = new ActivitySessionEngine(clock, randomSource);
        var logViewModel = new LogViewModel();
        var versionViewModel = new VersionViewModel();
        idleService = new Win32IdleService();

        viewModel = new MainViewModel(
            engine,
            idleService,
            new Win32ActivityPerformer(),
            new DispatcherTimerFactory(),
            logViewModel,
            versionViewModel);

        DataContext = viewModel;

        logWindow = new LogWindow
        {
            DataContext = logViewModel,
        };

        trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show Main Window", null, (_, _) => Dispatcher.Invoke(RestoreFromTray));
        trayMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Close));

        trayIconResource = LoadTrayIcon();
        trayIcon = new Forms.NotifyIcon
        {
            Text = "Activer",
            Icon = trayIconResource,
            ContextMenuStrip = trayMenu,
            Visible = false,
        };

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ShowVersionRequested += ViewModel_ShowVersionRequested;
        viewModel.MinimizeRequested += ViewModel_MinimizeRequested;
        trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;
    }

    #region Window Shadow & Dragging
    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var val = 2;
        DwmSetWindowAttribute(hwnd, 2, ref val, sizeof(int));
        var margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 1, cyBottomHeight = 0 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (logWindow.Owner is null)
        {
            logWindow.Owner = this;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }
    #endregion

    #region Window Closing
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        viewModel.ShowVersionRequested -= ViewModel_ShowVersionRequested;
        viewModel.MinimizeRequested -= ViewModel_MinimizeRequested;
        trayIcon.MouseDoubleClick -= TrayIcon_MouseDoubleClick;
        CleanupTrayIcon();
        viewModel.Dispose();
        idleService.Dispose();

        logWindow.ForceClose = true;
        logWindow.Close();

        WpfApplication.Current.Shutdown();
    }
    #endregion

    #region ViewModel Interaction
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CanShowLog))
        {
            UpdateLogWindowVisibility();
        }
    }

    private void ViewModel_ShowVersionRequested(object? sender, EventArgs e)
    {
        if (currentVersionWindow is not null)
        {
            return;
        }

        currentVersionWindow = new VersionWindow(viewModel.Version);
        currentVersionWindow.Owner = this;
        currentVersionWindow.Closed += (_, _) => currentVersionWindow = null;
        currentVersionWindow.Show();
    }

    private void ViewModel_MinimizeRequested(object? sender, EventArgs e)
    {
        HideToTray();
    }

    private void UpdateLogWindowVisibility()
    {
        if (isHiddenToTray || !viewModel.CanShowLog)
        {
            logWindow.Hide();
            return;
        }

        var logLeft = Left + Width + 10;
        var logTop = Top;
        var screenWidth = SystemParameters.WorkArea.Width;
        var screenHeight = SystemParameters.WorkArea.Height;

        if (logLeft + logWindow.Width > screenWidth)
        {
            logLeft = Math.Max(0, Left - logWindow.Width - 10);
        }

        if (logTop + logWindow.Height > screenHeight)
        {
            logTop = screenHeight - logWindow.Height;
        }

        logWindow.Left = logLeft;
        logWindow.Top = logTop;
        logWindow.Topmost = true;
        logWindow.Show();
    }

    private void TrayIcon_MouseDoubleClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            Dispatcher.Invoke(RestoreFromTray);
        }
    }

    private void HideToTray()
    {
        if (isHiddenToTray)
        {
            return;
        }

        isHiddenToTray = true;
        logWindow.Hide();
        trayIcon.Visible = true;
        Hide();
    }

    private void RestoreFromTray()
    {
        if (!isHiddenToTray)
        {
            Activate();
            return;
        }

        isHiddenToTray = false;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        trayIcon.Visible = false;
        UpdateLogWindowVisibility();
    }

    private void CleanupTrayIcon()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        trayMenu.Dispose();
        trayIconResource.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        var resourceInfo = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Resources/activer.ico"));
        if (resourceInfo?.Stream is null)
        {
            throw new InvalidOperationException("Tray icon resource 'Resources/activer.ico' was not found.");
        }

        using var iconStream = resourceInfo.Stream;
        using var icon = new Icon(iconStream);
        return (Icon)icon.Clone();
    }
    #endregion

    #region Input Validation
    private void NumberOnly(object sender, TextCompositionEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            e.Handled = true;
            return;
        }

        if (!e.Text.All(char.IsDigit))
        {
            e.Handled = true;
            return;
        }

        var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
        if (string.IsNullOrEmpty(newText))
        {
            e.Handled = false;
            return;
        }

        e.Handled = !int.TryParse(newText, out var value) || value < 1 || value > 999;
    }

    private void IntervalBox_LostFocus(object sender, RoutedEventArgs e)
    {
        viewModel.NormalizeIntervalInputs();
        RefreshBinding(IntervalMinBox, WpfTextBox.TextProperty);
        RefreshBinding(IntervalMaxBox, WpfTextBox.TextProperty);
    }

    private void NumberBox_LostFocus(object sender, RoutedEventArgs e)
    {
        viewModel.NormalizeIdleInput();
        RefreshBinding(sender, WpfTextBox.TextProperty);
    }

    private void TimeEndBox_LostFocus(object sender, RoutedEventArgs e)
    {
        viewModel.NormalizeEndTimeInput();
        RefreshBinding(sender, Xceed.Wpf.Toolkit.MaskedTextBox.TextProperty);
    }

    private static void RefreshBinding(object sender, DependencyProperty dependencyProperty)
    {
        if (sender is not DependencyObject dependencyObject)
        {
            return;
        }

        var binding = BindingOperations.GetBindingExpression(dependencyObject, dependencyProperty);
        binding?.UpdateTarget();
    }
    #endregion
}
