using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using Activer.ViewModels;

namespace Activer;

public partial class LogWindow : Window
{
    public bool ForceClose { get; set; }

    public LogWindow()
    {
        InitializeComponent();
        Closing += LogWindow_Closing;
        DataContextChanged += LogWindow_DataContextChanged;
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
