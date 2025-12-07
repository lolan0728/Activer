using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Activer
{
    public partial class VersionWindow : Window
    {
        public VersionWindow()
        {
            InitializeComponent();
            Loaded += VersionWindow_Loaded;
        }

        private void VersionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the screen work area (excluding taskbar)
            var workArea = SystemParameters.WorkArea;

            // Initial position at the bottom-right corner of the screen
            this.Left = workArea.Right - this.Width - 10;
            this.Top = workArea.Bottom - this.Height - 10;

            // Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
            fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            this.BeginAnimation(Window.OpacityProperty, fadeIn);

            // Move up animation by 20px
            var moveUp = new DoubleAnimation(this.Top, this.Top - 20, new Duration(TimeSpan.FromMilliseconds(300)));
            moveUp.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            this.BeginAnimation(Window.TopProperty, moveUp);

            // After 5 seconds, fade out and move up by 10px
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();

                var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(500)));
                fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                var moveOut = new DoubleAnimation(this.Top, this.Top - 10, new Duration(TimeSpan.FromMilliseconds(500)));
                moveOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                // Close window when fade-out completes
                fadeOut.Completed += (s2, e2) => this.Close();

                this.BeginAnimation(Window.OpacityProperty, fadeOut);
                this.BeginAnimation(Window.TopProperty, moveOut);
            };
            timer.Start();
        }
    }
}
