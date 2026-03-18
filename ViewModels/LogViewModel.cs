using System.Collections.ObjectModel;
using Activer.Mvvm;

namespace Activer.ViewModels;

public sealed class LogViewModel : ObservableObject
{
    private bool isVisible;
    private string runTimeText = "Run time: 00:00:00";
    private string endTimeText = "End Time: --";

    public LogViewModel()
    {
        ClearLogCommand = new RelayCommand(ClearEntries);
    }

    public ObservableCollection<string> Entries { get; } = new();

    public RelayCommand ClearLogCommand { get; }

    public bool IsVisible
    {
        get => isVisible;
        set => SetProperty(ref isVisible, value);
    }

    public string RunTimeText
    {
        get => runTimeText;
        set => SetProperty(ref runTimeText, value);
    }

    public string EndTimeText
    {
        get => endTimeText;
        set => SetProperty(ref endTimeText, value);
    }

    public void Append(string message)
    {
        Entries.Add(message);
    }

    private void ClearEntries()
    {
        Entries.Clear();
    }
}
