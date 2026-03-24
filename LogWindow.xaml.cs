using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Activer.ViewModels;

namespace Activer;

public partial class LogWindow : Window
{
    public bool ForceClose { get; set; }

    public LogWindow()
    {
        InitializeComponent();
        Loaded += LogWindow_Loaded;
        Closing += LogWindow_Closing;
        DataContextChanged += LogWindow_DataContextChanged;
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LogWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var val = 2;
        DwmSetWindowAttribute(hwnd, 2, ref val, sizeof(int));
        var margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 1, cyBottomHeight = 0 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
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

    private void LogWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LogViewModel oldViewModel)
        {
            oldViewModel.Entries.CollectionChanged -= Entries_CollectionChanged;
        }

        if (e.NewValue is LogViewModel newViewModel)
        {
            newViewModel.Entries.CollectionChanged += Entries_CollectionChanged;
        }
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null || e.NewItems.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => LogListBox.ScrollIntoView(e.NewItems[^1]));
    }

    private void LogWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ForceClose)
        {
            e.Cancel = true;
            Hide();

            if (Owner is MainWindow mainWindow && mainWindow.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.CanShowLog = false;
            }
            else if (DataContext is LogViewModel logViewModel)
            {
                logViewModel.IsVisible = false;
            }
        }
    }
}
