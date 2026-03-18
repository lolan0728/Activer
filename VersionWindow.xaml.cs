using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Activer.ViewModels;

namespace Activer;

public partial class VersionWindow : Window
{
    public VersionWindow(VersionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += VersionWindow_Loaded;
    }

    private void VersionWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;

        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;

        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(OpacityProperty, fadeIn);

        var moveUp = new DoubleAnimation(Top, Top - 20, new Duration(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(TopProperty, moveUp);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(500)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            var moveOut = new DoubleAnimation(Top, Top - 10, new Duration(TimeSpan.FromMilliseconds(500)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };

            fadeOut.Completed += (_, _) => Close();

            BeginAnimation(OpacityProperty, fadeOut);
            BeginAnimation(TopProperty, moveOut);
        };
        timer.Start();
    }
}
