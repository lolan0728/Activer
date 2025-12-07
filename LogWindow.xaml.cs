using System;
using System.Windows;

namespace Activer
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        // Append a log entry at the end and scroll to the bottom
        public void AppendLog(string text)
        {
            if (LogTextBox == null) return;
            LogTextBox.AppendText(text + "\n");
            LogTextBox.ScrollToEnd();
        }

        // Update the run time display (does not append to log)
        public void UpdateRunTime(TimeSpan t)
        {
            if (RunTimeTextBlock == null) return;
            RunTimeTextBlock.Text = $"Run time: {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }

        // Update the End Time display
        public void UpdateEndTime(DateTime? endTime)
        {
            if (endTime.HasValue)
                EndTimeTextBlock.Text = $"End Time: {endTime.Value:yyyy/MM/dd HH:mm:ss}";
            else
                EndTimeTextBlock.Text = "End Time: --";
        }

        // Handle Clear button click
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }
    }
}
