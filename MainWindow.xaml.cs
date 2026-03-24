using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Activer.Core.Engine;
using Activer.Core.Services;
using Activer.Services;
using Activer.ViewModels;
using Microsoft.Win32;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Activer;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly Win32IdleService idleService;
    private readonly LogWindow logWindow;
    private readonly TrayMenuWindow trayMenuWindow;
    private readonly DrawingIcon trayIdleIconResource;
    private readonly DrawingIcon trayRunningIconResource;
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

        trayMenuWindow = new TrayMenuWindow();
        trayMenuWindow.ShowWindowRequested += TrayMenuWindow_ShowWindowRequested;
        trayMenuWindow.StartStopRequested += TrayMenuWindow_StartStopRequested;
        trayMenuWindow.QuitRequested += TrayMenuWindow_QuitRequested;

        trayIdleIconResource = LoadTrayIcon();
        trayRunningIconResource = LoadRunningTrayIcon(trayIdleIconResource);
        trayIcon = new Forms.NotifyIcon
        {
            Text = "Activer",
            Icon = trayIdleIconResource,
            Visible = true,
        };

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ShowVersionRequested += ViewModel_ShowVersionRequested;
        viewModel.MinimizeRequested += ViewModel_MinimizeRequested;
        trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;
        trayIcon.MouseUp += TrayIcon_MouseUp;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        UpdateTrayVisuals();
        UpdateTrayTheme();
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
        trayIcon.MouseUp -= TrayIcon_MouseUp;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        trayMenuWindow.ShowWindowRequested -= TrayMenuWindow_ShowWindowRequested;
        trayMenuWindow.StartStopRequested -= TrayMenuWindow_StartStopRequested;
        trayMenuWindow.QuitRequested -= TrayMenuWindow_QuitRequested;
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
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.StartStopText) || e.PropertyName == nameof(MainViewModel.IsRunning))
        {
            UpdateTrayVisuals();
        }
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateTrayTheme);
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

    private void TrayMenuWindow_ShowWindowRequested(object? sender, EventArgs e)
    {
        RestoreFromTray();
    }

    private void TrayMenuWindow_StartStopRequested(object? sender, EventArgs e)
    {
        ToggleSessionFromTray();
    }

    private void TrayMenuWindow_QuitRequested(object? sender, EventArgs e)
    {
        Close();
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
            trayMenuWindow.Hide();
            Dispatcher.Invoke(RestoreFromTray);
        }
    }

    private void TrayIcon_MouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Right)
        {
            Dispatcher.Invoke(ToggleTrayMenuVisibility);
        }
    }

    private void ToggleTrayMenuVisibility()
    {
        if (trayMenuWindow.IsVisible)
        {
            trayMenuWindow.Hide();
            return;
        }

        UpdateTrayTheme();
        UpdateTrayVisuals();
        trayMenuWindow.ShowAt(GetTrayMenuPosition());
        trayMenuWindow.ShowAt(GetTrayMenuPosition());
    }

    private void ToggleSessionFromTray()
    {
        if (!viewModel.StartStopCommand.CanExecute(null))
        {
            return;
        }

        viewModel.StartStopCommand.Execute(null);
        UpdateTrayVisuals();
    }

    private void UpdateTrayVisuals()
    {
        trayMenuWindow.UpdateState(
            viewModel.StartStopText,
            viewModel.StartStopCommand.CanExecute(null),
            viewModel.IsRunning);

        trayIcon.Icon = viewModel.IsRunning ? trayRunningIconResource : trayIdleIconResource;
    }

    private void UpdateTrayTheme()
    {
        trayMenuWindow.UpdateTheme(IsWindowsUsingLightTheme());
    }

    private void HideToTray()
    {
        if (isHiddenToTray)
        {
            return;
        }

        isHiddenToTray = true;
        trayMenuWindow.Hide();
        logWindow.Hide();
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
        trayMenuWindow.Hide();
        Show();
        WindowState = WindowState.Normal;
        Activate();
        UpdateLogWindowVisibility();
    }

    private void CleanupTrayIcon()
    {
        trayMenuWindow.ForceClose = true;
        trayMenuWindow.Close();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        trayIdleIconResource.Dispose();
        trayRunningIconResource.Dispose();
    }

    private System.Windows.Point GetTrayMenuPosition()
    {
        var cursorPosition = Forms.Cursor.Position;
        var screenBounds = Forms.Screen.FromPoint(cursorPosition).WorkingArea;
        var transformFromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        var cursor = transformFromDevice.Transform(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));
        var screenTopLeft = transformFromDevice.Transform(new System.Windows.Point(screenBounds.Left, screenBounds.Top));
        var screenBottomRight = transformFromDevice.Transform(new System.Windows.Point(screenBounds.Right, screenBounds.Bottom));

        var horizontalPadding = 8d;
        var verticalPadding = 8d;
        var left = cursor.X - trayMenuWindow.MenuWidth + 12;
        var top = cursor.Y - trayMenuWindow.MenuHeight - 8;

        left = Math.Max(screenTopLeft.X + horizontalPadding, Math.Min(left, screenBottomRight.X - trayMenuWindow.MenuWidth - horizontalPadding));
        top = Math.Max(screenTopLeft.Y + verticalPadding, Math.Min(top, screenBottomRight.Y - trayMenuWindow.MenuHeight - verticalPadding));

        return new System.Windows.Point(left, top);
    }

    private static DrawingIcon LoadTrayIcon()
    {
        var resourceInfo = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Resources/activer.ico"));
        if (resourceInfo?.Stream is null)
        {
            throw new InvalidOperationException("Tray icon resource 'Resources/activer.ico' was not found.");
        }

        using var iconStream = resourceInfo.Stream;
        using var icon = new DrawingIcon(iconStream);
        return (DrawingIcon)icon.Clone();
    }

    private static DrawingIcon LoadRunningTrayIcon(DrawingIcon sourceIcon)
    {
        using var baseBitmap = sourceIcon.ToBitmap();
        using var recoloredBitmap = RecolorTrayBitmap(baseBitmap);
        var iconHandle = recoloredBitmap.GetHicon();

        try
        {
            using var icon = DrawingIcon.FromHandle(iconHandle);
            return (DrawingIcon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static DrawingBitmap RecolorTrayBitmap(DrawingBitmap source)
    {
        var recolored = new DrawingBitmap(source.Width, source.Height);

        for (var x = 0; x < source.Width; x++)
        {
            for (var y = 0; y < source.Height; y++)
            {
                var pixel = source.GetPixel(x, y);
                recolored.SetPixel(x, y, ShiftPixelHueToRed(pixel));
            }
        }

        return recolored;
    }

    private static DrawingColor ShiftPixelHueToRed(DrawingColor color)
    {
        if (color.A == 0)
        {
            return color;
        }

        RgbToHsv(color, out var hue, out var saturation, out var value);

        if (saturation < 0.05)
        {
            return color;
        }

        return HsvToRgb(358d, saturation, value, color.A);
    }

    private static void RgbToHsv(DrawingColor color, out double hue, out double saturation, out double value)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        hue = 0d;

        if (delta > 0d)
        {
            if (Math.Abs(max - red) < double.Epsilon)
            {
                hue = 60d * (((green - blue) / delta) % 6d);
            }
            else if (Math.Abs(max - green) < double.Epsilon)
            {
                hue = 60d * (((blue - red) / delta) + 2d);
            }
            else
            {
                hue = 60d * (((red - green) / delta) + 4d);
            }
        }

        if (hue < 0d)
        {
            hue += 360d;
        }

        saturation = max <= 0d ? 0d : delta / max;
        value = max;
    }

    private static DrawingColor HsvToRgb(double hue, double saturation, double value, int alpha)
    {
        var chroma = value * saturation;
        var hueSection = hue / 60d;
        var secondLargest = chroma * (1d - Math.Abs((hueSection % 2d) - 1d));
        var match = value - chroma;

        var (red, green, blue) = hueSection switch
        {
            >= 0d and < 1d => (chroma, secondLargest, 0d),
            >= 1d and < 2d => (secondLargest, chroma, 0d),
            >= 2d and < 3d => (0d, chroma, secondLargest),
            >= 3d and < 4d => (0d, secondLargest, chroma),
            >= 4d and < 5d => (secondLargest, 0d, chroma),
            _ => (chroma, 0d, secondLargest),
        };

        return DrawingColor.FromArgb(
            alpha,
            (int)Math.Round((red + match) * 255d),
            (int)Math.Round((green + match) * 255d),
            (int)Math.Round((blue + match) * 255d));
    }

    private static bool IsWindowsUsingLightTheme()
    {
        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = personalizeKey?.GetValue("AppsUseLightTheme");

            if (value is int intValue)
            {
                return intValue != 0;
            }

            if (value is not null)
            {
                return Convert.ToInt32(value) != 0;
            }
        }
        catch
        {
        }

        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
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
