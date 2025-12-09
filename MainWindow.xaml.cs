using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;

namespace Activer
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private Random random = new Random();
        private int actionCount = 0;
        private LogWindow? logWindow;
        private VersionWindow? currentVersionWindow = null;

        private DispatcherTimer runTimeTimer;
        private DateTime startTime;
        private TimeSpan accumulatedRunTime = TimeSpan.Zero;

        private DateTime? targetEndTime = null;

        // -------------------- New control variables --------------------
        private DateTime lastActionTime;
        private int nextIntervalSeconds;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;

            logWindow = new LogWindow();
            logWindow.Hide();

            ShowLogCheckBox.Checked += ShowLogCheckBox_Checked;
            ShowLogCheckBox.Unchecked += ShowLogCheckBox_Unchecked;

            runTimeTimer = new DispatcherTimer();
            runTimeTimer.Interval = TimeSpan.FromSeconds(1);
            runTimeTimer.Tick += RunTimeTimer_Tick;

            StartStopButton.Checked += StartStopButton_Checked;
            StartStopButton.Unchecked += StartStopButton_Unchecked;

            this.Closing += MainWindow_Closing;
        }

        #region Window Shadow & Dragging
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            int val = 2;
            DwmSetWindowAttribute(hwnd, 2, ref val, sizeof(int));

            var margins = new MARGINS() { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 1, cyBottomHeight = 0 };
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
        #endregion

        #region Window Closing
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logWindow?.Close();
            logWindow = null;

            Application.Current.Shutdown();
        }
        #endregion

        #region Version Window
        private void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentVersionWindow != null)
                return;

            currentVersionWindow = new VersionWindow();
            currentVersionWindow.Closed += (s, args) => currentVersionWindow = null;
            currentVersionWindow.Show();
        }
        #endregion

        #region Show Log Window
        private void ShowLogCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (logWindow != null)
            {
                var mainLeft = this.Left;
                var mainTop = this.Top;
                var mainWidth = this.Width;
                var mainHeight = this.Height;

                var screenWidth = SystemParameters.WorkArea.Width;
                var screenHeight = SystemParameters.WorkArea.Height;

                double logLeft = mainLeft + mainWidth + 10;
                if (logLeft + logWindow.Width > screenWidth)
                {
                    logLeft = mainLeft - logWindow.Width - 10;
                    if (logLeft < 0) logLeft = 0;
                }

                double logTop = mainTop;
                if (logTop + logWindow.Height > screenHeight)
                    logTop = screenHeight - logWindow.Height;

                logWindow.Left = logLeft;
                logWindow.Top = logTop;
                logWindow.Topmost = true;

                logWindow.Show();
            }
        }

        private void ShowLogCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            logWindow?.Hide();
        }
        #endregion

        #region Number Input Validation
        private void NumberOnly(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) { e.Handled = true; return; }

            if (!e.Text.All(char.IsDigit)) { e.Handled = true; return; }

            string newText = tb.Text.Insert(tb.SelectionStart, e.Text);
            if (string.IsNullOrEmpty(newText)) { e.Handled = false; return; }

            if (!int.TryParse(newText, out int value)) { e.Handled = true; return; }
            if (value < 1 || value > 999) { e.Handled = true; return; }

            e.Handled = false;
        }

        private void NumberBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out int value) || value < 1)
                    tb.Text = "1";
                else if (value > 999)
                    tb.Text = "999";
            }
        }

        private void IntervalBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(IntervalMinBox.Text, out int min)) { min = 1; IntervalMinBox.Text = "1"; }
            if (!int.TryParse(IntervalMaxBox.Text, out int max)) { max = 1; IntervalMaxBox.Text = "1"; }

            min = Math.Clamp(min, 1, 999);
            max = Math.Clamp(max, 1, 999);

            if (min > max) (min, max) = (max, min);

            IntervalMinBox.Text = min.ToString();
            IntervalMaxBox.Text = max.ToString();
        }

        private void TimeEndBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not Xceed.Wpf.Toolkit.MaskedTextBox tb) return;

            var parts = tb.Text.Split(':');
            if (parts.Length != 3) { tb.Text = "00:00:00"; return; }

            int h = int.TryParse(parts[0], out int hh) ? hh : 0;
            int m = int.TryParse(parts[1], out int mm) ? mm : 0;
            int s = int.TryParse(parts[2], out int ss) ? ss : 0;

            h = Math.Clamp(h, 0, 23);
            m = Math.Clamp(m, 0, 59);
            s = Math.Clamp(s, 0, 59);

            tb.Text = $"{h:00}:{m:00}:{s:00}";
        }
        #endregion

        #region Start/Stop Timer & RunTime
        private void StartStopButton_Checked(object sender, RoutedEventArgs e)
        {
            startTime = DateTime.Now;
            actionCount = 0;
            accumulatedRunTime = TimeSpan.Zero;

            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1); // Check every second
                timer.Tick += Timer_Tick;
            }

            runTimeTimer.Start();

            if (EnableTimeInputCheckBox.IsChecked == true &&
                !string.IsNullOrWhiteSpace(TimeEndBox.Text) &&
                DateTime.TryParse(TimeEndBox.Text, out DateTime parsedTime))
            {
                DateTime now = DateTime.Now;
                DateTime todayEndTime = new DateTime(now.Year, now.Month, now.Day,
                                                    parsedTime.Hour, parsedTime.Minute, parsedTime.Second);
                if (now >= todayEndTime) todayEndTime = todayEndTime.AddDays(1);

                targetEndTime = todayEndTime;
            }
            else
            {
                targetEndTime = null;
            }

            logWindow.UpdateEndTime(targetEndTime);
            logWindow.AppendLog($"[{startTime:HH:mm:ss}] Activity started at {startTime:HH:mm:ss}");

            // -------------------- Initialize action timer --------------------
            lastActionTime = DateTime.Now;
            nextIntervalSeconds = random.Next(ParsePositiveInt(IntervalMinBox.Text, 10),
                                              ParsePositiveInt(IntervalMaxBox.Text, 60) + 1);

            timer.Start();
            logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] Next activity in {nextIntervalSeconds} seconds");
        }

        private void StartStopButton_Unchecked(object sender, RoutedEventArgs e)
        {
            timer?.Stop();
            runTimeTimer.Stop();
            targetEndTime = null;
            logWindow.UpdateEndTime(null);

            TimeSpan totalRunTime = DateTime.Now - startTime;
            logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] Activity stopped, total run time: {totalRunTime:hh\\:mm\\:ss}");
            logWindow.UpdateRunTime(totalRunTime);
        }

        private void RunTimeTimer_Tick(object sender, EventArgs e)
        {
            if (StartStopButton.IsChecked == true)
            {
                TimeSpan runTime = DateTime.Now - startTime;
                logWindow.UpdateRunTime(runTime);

                if (targetEndTime.HasValue && DateTime.Now >= targetEndTime.Value)
                {
                    StartStopButton.IsChecked = false;
                    logWindow.AppendLog(
                        $"[{DateTime.Now:HH:mm:ss}] Reached End Time ({targetEndTime:yyyy/MM/dd HH:mm:ss}), stopping activity automatically.");
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            int idleSeconds = GetIdleSeconds();

            if (idleSeconds == 0)
            {
                // User operated mouse/keyboard
                lastActionTime = DateTime.Now;
                logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] User activity detected, resetting idle timer");
                return;
            }

            // Check if it's time for next action
            if ((DateTime.Now - lastActionTime).TotalSeconds >= nextIntervalSeconds)
            {
                PerformActivity();
                lastActionTime = DateTime.Now;

                // Calculate next interval
                nextIntervalSeconds = random.Next(ParsePositiveInt(IntervalMinBox.Text, 10),
                                                  ParsePositiveInt(IntervalMaxBox.Text, 60) + 1);

                logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] Next activity in {nextIntervalSeconds} seconds");
            }
        }

        private int ParsePositiveInt(string text, int defaultValue)
        {
            return int.TryParse(text, out int value) && value > 0 ? value : defaultValue;
        }
        #endregion

        #region Win32 API for Idle & Mouse
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_SHIFT = 0x10;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        private int GetIdleSeconds()
        {
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);
            uint tickCount = (uint)Environment.TickCount;
            return (int)((tickCount - lastInput.dwTime) / 1000);
        }

        private void PerformActivity()
        {
            if (!GetCursorPos(out POINT original)) return;

            actionCount++;
            string nowStr = DateTime.Now.ToString("HH:mm:ss");

            int offsetX = random.Next(-10, 11);
            int offsetY = random.Next(-10, 11);

            logWindow.AppendLog($"[{nowStr}] Action #{actionCount} - Original position: X={original.X}, Y={original.Y}, Offset=({offsetX},{offsetY})");

            // Smoothly move the mouse
            SmoothMove(original.X, original.Y, original.X + offsetX, original.Y + offsetY, 10, 20);
            SmoothMove(original.X + offsetX, original.Y + offsetY, original.X, original.Y, 10, 20);

            // Randomly press a combo key (Shift, Ctrl, Alt)
            byte[] comboKeys = new byte[] { 0x10 /*Shift*/, 0x11 /*Ctrl*/, 0x12 /*Alt*/ };
            byte key = comboKeys[random.Next(comboKeys.Length)];

            keybd_event(key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50 + random.Next(50));
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            logWindow.AppendLog($"[{nowStr}] Action #{actionCount} completed - combo key {key} pressed and released");
        }

        private void SmoothMove(int startX, int startY, int endX, int endY, int steps, int delayMs)
        {
            for (int i = 1; i <= steps; i++)
            {
                int x = startX + (endX - startX) * i / steps;
                int y = startY + (endY - startY) * i / steps;
                SetCursorPos(x, y);
                Thread.Sleep(delayMs);
            }
        }
        #endregion
    }

    public class StartStopConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isChecked = value is bool b && b;
            return isChecked ? "Stop" : "Start";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
