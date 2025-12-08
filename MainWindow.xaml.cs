using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Activer
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private Random random = new Random();
        private int actionCount = 0;
        private LogWindow? logWindow;
        private VersionWindow? currentVersionWindow = null;

        // Run Time auto-refresh
        private DispatcherTimer runTimeTimer;
        private DateTime startTime;
        private TimeSpan accumulatedRunTime = TimeSpan.Zero;

        // New field: target end time
        private DateTime? targetEndTime = null;

        public MainWindow()
        {
            InitializeComponent();

            // Create log window
            logWindow = new LogWindow();
            logWindow.Hide();

            ShowLogCheckBox.Checked += ShowLogCheckBox_Checked;
            ShowLogCheckBox.Unchecked += ShowLogCheckBox_Unchecked;

            // Initialize RunTimeTimer
            runTimeTimer = new DispatcherTimer();
            runTimeTimer.Interval = TimeSpan.FromSeconds(1);
            runTimeTimer.Tick += RunTimeTimer_Tick;

            StartStopButton.Checked += StartStopButton_Checked;
            StartStopButton.Unchecked += StartStopButton_Unchecked;

            this.Closing += MainWindow_Closing;
        }

        // ======================================================
        // Close the application completely when clicking X or closing the window
        // ======================================================
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logWindow?.Close();
            logWindow = null;

            Application.Current.Shutdown();
        }

        private void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentVersionWindow != null)
                return;

            currentVersionWindow = new VersionWindow();
            currentVersionWindow.Closed += (s, args) => currentVersionWindow = null;
            currentVersionWindow.Show();
        }

        // ======================================================
        // Show log window
        // ======================================================
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

        // Only allow numeric input
        private void NumberOnly(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                e.Handled = true;
                return;
            }

            // 输入的必须是数字
            if (!e.Text.All(char.IsDigit))
            {
                e.Handled = true;
                return;
            }

            // 用户当前的文本 + 本次输入
            string newText = tb.Text.Insert(tb.SelectionStart, e.Text);

            // 空值允许，等用户继续输入
            if (string.IsNullOrEmpty(newText))
            {
                e.Handled = false;
                return;
            }

            // 检查是否是合法整数
            if (!int.TryParse(newText, out int value))
            {
                e.Handled = true;
                return;
            }

            // 限制范围 1 ~ 999
            if (value < 1 || value > 999)
            {
                e.Handled = true;  // 超范围 → 阻止输入
                return;
            }

            // 合法 → 允许输入
            e.Handled = false;
        }

        private void NumberBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out int value) || value < 1)
                {
                    tb.Text = "1"; // 空值或非法 → 自动变 1
                }
                else if (value > 999)
                {
                    tb.Text = "999";
                }
            }
        }

        private void IntervalBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(IntervalMinBox.Text, out int min))
            {
                IntervalMinBox.Text = "1";
                min = 1;
            }

            if (!int.TryParse(IntervalMaxBox.Text, out int max))
            {
                IntervalMaxBox.Text = "1";
                max = 1;
            }

            // 矫正范围 1~999（你 NumberOnly 已经限制，但这里双保险）
            min = Math.Clamp(min, 1, 999);
            max = Math.Clamp(max, 1, 999);

            // 自动交换，确保 min ≤ max
            if (min > max)
            {
                int temp = min;
                min = max;
                max = temp;
            }

            IntervalMinBox.Text = min.ToString();
            IntervalMaxBox.Text = max.ToString();
        }

        private void TimeEndBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not Xceed.Wpf.Toolkit.MaskedTextBox tb) return;

            string raw = tb.Text;

            // 按冒号拆分
            var parts = raw.Split(':');
            if (parts.Length != 3)
            {
                tb.Text = "00:00:00";
                return;
            }

            // 尝试解析每一段，不合法的都按 0 处理
            int h = int.TryParse(parts[0], out int hh) ? hh : 0;
            int m = int.TryParse(parts[1], out int mm) ? mm : 0;
            int s = int.TryParse(parts[2], out int ss) ? ss : 0;

            // 修正范围
            h = Math.Clamp(h, 0, 23);
            m = Math.Clamp(m, 0, 59);
            s = Math.Clamp(s, 0, 59);

            // 格式化并写回
            tb.Text = $"{h:00}:{m:00}:{s:00}";
        }

        // ======================================================
        // Start / Stop logic
        // ======================================================
        private void StartStopButton_Checked(object sender, RoutedEventArgs e)
        {
            startTime = DateTime.Now;
            actionCount = 0;
            accumulatedRunTime = TimeSpan.Zero;

            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Tick += Timer_Tick;
            }

            timer.Stop();
            runTimeTimer.Start();

            // ----------------------------
            // Set end time according to EnableTimeInputCheckBox
            // ----------------------------
            if (EnableTimeInputCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(TimeEndBox.Text)
                && DateTime.TryParse(TimeEndBox.Text, out DateTime parsedTime))
            {
                DateTime now = DateTime.Now;
                DateTime todayEndTime = new DateTime(now.Year, now.Month, now.Day,
                                                    parsedTime.Hour, parsedTime.Minute, 0);
                // If current time has passed today's end time, move it to tomorrow
                if (now >= todayEndTime)
                    todayEndTime = todayEndTime.AddDays(1);

                targetEndTime = todayEndTime;
            }
            else
            {
                // Not checked or no time set → never ends automatically
                targetEndTime = null;
            }

            // Update log window to show end time
            logWindow.UpdateEndTime(targetEndTime);

            logWindow.AppendLog($"[{startTime:HH:mm:ss}] Activity started at {startTime:HH:mm:ss}");
            ScheduleNextTick(); // Schedule first activity
        }

        private void StartStopButton_Unchecked(object sender, RoutedEventArgs e)
        {
            timer?.Stop();
            runTimeTimer.Stop();

            // When manually stopped, clear targetEndTime and update log display
            targetEndTime = null;
            logWindow.UpdateEndTime(null);

            TimeSpan totalRunTime = DateTime.Now - startTime;
            logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] Activity stopped, total run time: {totalRunTime:hh\\:mm\\:ss}");
            logWindow.UpdateRunTime(totalRunTime);
        }

        // ======================================================
        // Run Time refresh every second + stop automatically at end time
        // ======================================================
        private void RunTimeTimer_Tick(object sender, EventArgs e)
        {
            if (StartStopButton.IsChecked == true)
            {
                TimeSpan runTime = DateTime.Now - startTime;
                logWindow.UpdateRunTime(runTime);

                if (targetEndTime.HasValue && DateTime.Now >= targetEndTime.Value)
                {
                    DateTime endTime = targetEndTime.Value;
                    StartStopButton.IsChecked = false;
                    logWindow.AppendLog(
                        $"[{DateTime.Now:HH:mm:ss}] Reached End Time ({endTime:yyyy/MM/dd HH:mm:ss}), stopping activity automatically.");
                }
            }
        }

        // ======================================================
        // Timer Tick for performing activity when idle
        // ======================================================
        private void Timer_Tick(object sender, EventArgs e)
        {
            int idleSeconds = ParsePositiveInt(IdleSecondsBox.Text, 30);
            if (GetIdleSeconds() >= idleSeconds)
            {
                PerformActivity();
                ScheduleNextTick();
            }
        }

        // Schedule next activity tick randomly between min and max seconds
        private void ScheduleNextTick()
        {
            int min = ParsePositiveInt(IntervalMinBox.Text, 10);
            int max = ParsePositiveInt(IntervalMaxBox.Text, 60);
            if (min > max) (min, max) = (max, min);

            int interval = random.Next(min, max + 1);
            timer.Interval = TimeSpan.FromSeconds(interval);
            timer.Start();

            logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] Next activity in {interval} seconds");
        }

        private int ParsePositiveInt(string text, int defaultValue)
        {
            return int.TryParse(text, out int value) && value > 0 ? value : defaultValue;
        }

        #region Win32 API (mouse + idle)
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_SHIFT = 0x10;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        // Get idle seconds since last input
        private int GetIdleSeconds()
        {
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);

            uint tickCount = (uint)Environment.TickCount;
            return (int)((tickCount - lastInput.dwTime) / 1000);
        }

        // Perform activity: move mouse slightly and press a random key
        private void PerformActivity()
        {
            if (!GetCursorPos(out POINT original)) return;

            actionCount++;
            TimeSpan runDuration = DateTime.Now - startTime;
            string nowStr = DateTime.Now.ToString("HH:mm:ss");

            int offsetX = random.Next(-10, 11);
            int offsetY = random.Next(-10, 11);

            logWindow.AppendLog($"[{nowStr}] Action #{actionCount} - Original position: X={original.X}, Y={original.Y}, Offset=({offsetX},{offsetY})");

            SmoothMove(original.X, original.Y, original.X + offsetX, original.Y + offsetY, 10, 20);
            SmoothMove(original.X + offsetX, original.Y + offsetY, original.X, original.Y, 10, 20);

            byte[] keys = new byte[] { VK_SHIFT, 0x41, 0x42, 0x43 };
            byte key = keys[random.Next(keys.Length)];
            keybd_event(key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50 + random.Next(50));
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            logWindow.AppendLog($"[{nowStr}] Action #{actionCount} completed - key {key} pressed and released");
        }

        // Smoothly move the cursor from start to end in steps
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
