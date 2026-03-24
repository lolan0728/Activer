using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace Activer;

public partial class TrayMenuWindow : Window
{
    private static readonly Geometry StartGeometry = Geometry.Parse("M2,1.5 L12.5,7 L2,12.5 Z");
    private static readonly Geometry StopGeometry = Geometry.Parse("M2,2 H12 V12 H2 Z");
    private bool? isUsingLightTheme;

    public TrayMenuWindow()
    {
        InitializeComponent();
        Deactivated += TrayMenuWindow_Deactivated;
        PreviewKeyDown += TrayMenuWindow_PreviewKeyDown;
    }

    public bool ForceClose { get; set; }

    public double MenuWidth => ActualWidth > 0 ? ActualWidth : double.IsNaN(Width) ? 164 : Width;

    public double MenuHeight => ActualHeight > 0 ? ActualHeight : 112;

    public event EventHandler? ShowWindowRequested;

    public event EventHandler? StartStopRequested;

    public event EventHandler? QuitRequested;

    public void ShowAt(System.Windows.Point position)
    {
        Left = position.X;
        Top = position.Y;

        if (!IsVisible)
        {
            Show();
        }

        Activate();
    }

    public void UpdateState(string startStopText, bool isEnabled, bool isRunning)
    {
        StartStopTextBlock.Text = startStopText;
        StartStopButton.IsEnabled = isEnabled;
        StartStopIconPath.Data = isRunning ? StopGeometry : StartGeometry;
    }

    public void UpdateTheme(bool useLightTheme)
    {
        if (isUsingLightTheme == useLightTheme)
        {
            return;
        }

        isUsingLightTheme = useLightTheme;

        if (useLightTheme)
        {
            SetBrush("MenuSurfaceBrush", MediaColor.FromRgb(249, 250, 252));
            SetBrush("MenuBorderBrush", MediaColor.FromRgb(214, 220, 228));
            SetBrush("MenuForegroundBrush", MediaColor.FromRgb(31, 37, 45));
            SetBrush("MenuHoverBrush", MediaColor.FromRgb(236, 240, 245));
            SetBrush("MenuPressedBrush", MediaColor.FromRgb(226, 231, 238));
            SetBrush("MenuDividerBrush", MediaColor.FromRgb(225, 229, 236));
            return;
        }

        SetBrush("MenuSurfaceBrush", MediaColor.FromRgb(31, 34, 40));
        SetBrush("MenuBorderBrush", MediaColor.FromRgb(52, 58, 68));
        SetBrush("MenuForegroundBrush", MediaColor.FromRgb(245, 247, 250));
        SetBrush("MenuHoverBrush", MediaColor.FromRgb(44, 49, 58));
        SetBrush("MenuPressedBrush", MediaColor.FromRgb(53, 59, 70));
        SetBrush("MenuDividerBrush", MediaColor.FromRgb(52, 57, 67));
    }

    private void ShowWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        StartStopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TrayMenuWindow_Deactivated(object? sender, EventArgs e)
    {
        Hide();
    }

    private void TrayMenuWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ForceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void SetBrush(string resourceKey, MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[resourceKey] = brush;
    }
}
