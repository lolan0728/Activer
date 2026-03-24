using Activer.Core.Engine;
using Activer.Core.Models;
using Activer.Core.Services;
using Activer.Mvvm;

namespace Activer.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ActivitySessionEngine sessionEngine;
    private readonly IIdleService idleService;
    private readonly IActivityPerformer activityPerformer;
    private readonly ITimer sessionTimer;

    private bool isRunning;
    private bool canShowLog;
    private bool isEndTimeEnabled = true;
    private bool isTickInProgress;
    private int actionCount;
    private string intervalMin = ActivitySettings.DefaultIntervalMinSeconds.ToString();
    private string intervalMax = ActivitySettings.DefaultIntervalMaxSeconds.ToString();
    private string idleSeconds = ActivitySettings.DefaultIdleSeconds.ToString();
    private string endTimeText = ActivitySettings.DefaultEndTimeText;
    private string runTimeText = "00:00:00";

    public MainViewModel(
        ActivitySessionEngine sessionEngine,
        IIdleService idleService,
        IActivityPerformer activityPerformer,
        ITimerFactory timerFactory,
        LogViewModel logViewModel,
        VersionViewModel versionViewModel)
    {
        this.sessionEngine = sessionEngine;
        this.idleService = idleService;
        this.activityPerformer = activityPerformer;

        Log = logViewModel;
        Version = versionViewModel;

        sessionTimer = timerFactory.CreateTimer();
        sessionTimer.Interval = TimeSpan.FromSeconds(1);
        sessionTimer.Tick += SessionTimer_Tick;

        StartStopCommand = new RelayCommand(ToggleSession);
        ShowVersionCommand = new RelayCommand(() => ShowVersionRequested?.Invoke(this, EventArgs.Empty));
        CloseCommand = new RelayCommand(() => MinimizeRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? MinimizeRequested;

    public event EventHandler? ShowVersionRequested;

    public RelayCommand StartStopCommand { get; }

    public RelayCommand ShowVersionCommand { get; }

    public RelayCommand CloseCommand { get; }

    public LogViewModel Log { get; }

    public VersionViewModel Version { get; }

    public string IntervalMin
    {
        get => intervalMin;
        set => SetProperty(ref intervalMin, value);
    }

    public string IntervalMax
    {
        get => intervalMax;
        set => SetProperty(ref intervalMax, value);
    }

    public string IdleSeconds
    {
        get => idleSeconds;
        set => SetProperty(ref idleSeconds, value);
    }

    public bool IsEndTimeEnabled
    {
        get => isEndTimeEnabled;
        set => SetProperty(ref isEndTimeEnabled, value);
    }

    public string EndTimeText
    {
        get => endTimeText;
        set => SetProperty(ref endTimeText, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        set
        {
            if (SetProperty(ref isRunning, value))
            {
                OnPropertyChanged(nameof(StartStopText));
            }
        }
    }

    public string RunTimeText
    {
        get => runTimeText;
        private set => SetProperty(ref runTimeText, value);
    }

    public bool CanShowLog
    {
        get => canShowLog;
        set
        {
            if (SetProperty(ref canShowLog, value))
            {
                Log.IsVisible = value;
            }
        }
    }

    public string StartStopText => IsRunning ? "Stop" : "Start";

    public int ActionCount
    {
        get => actionCount;
        private set => SetProperty(ref actionCount, value);
    }

    public void NormalizeIntervalInputs()
    {
        var settings = CreateSettings();
        IntervalMin = settings.IntervalMinSeconds.ToString();
        IntervalMax = settings.IntervalMaxSeconds.ToString();
    }

    public void NormalizeIdleInput()
    {
        var settings = CreateSettings();
        IdleSeconds = settings.IdleThresholdSeconds.ToString();
    }

    public void NormalizeEndTimeInput()
    {
        var settings = CreateSettings();
        EndTimeText = settings.EndTimeText;
    }

    public void Dispose()
    {
        sessionTimer.Tick -= SessionTimer_Tick;
        sessionTimer.Dispose();
    }

    private void SessionTimer_Tick(object? sender, EventArgs e)
    {
        if (isTickInProgress || !sessionEngine.IsRunning)
        {
            return;
        }

        isTickInProgress = true;

        try
        {
            var update = sessionEngine.Tick(idleService.GetIdleSeconds());
            ApplyUpdate(update);
        }
        finally
        {
            isTickInProgress = false;
        }
    }

    private void ToggleSession()
    {
        if (sessionEngine.IsRunning)
        {
            StopSession();
            return;
        }

        StartSession();
    }

    private void StartSession()
    {
        NormalizeAllInputs();
        var update = sessionEngine.Start(CreateSettings());
        sessionTimer.Start();
        ApplyUpdate(update);
    }

    private void StopSession()
    {
        var update = sessionEngine.Stop();
        sessionTimer.Stop();
        ApplyUpdate(update);
    }

    private void ApplyUpdate(ActivitySessionUpdate update)
    {
        IsRunning = update.IsRunning;
        ActionCount = update.ActionCount;
        RunTimeText = $"{update.RunTime:hh\\:mm\\:ss}";
        Log.RunTimeText = $"Run time: {RunTimeText}";
        Log.EndTimeText = update.TargetEndTime.HasValue
            ? $"End Time: {update.TargetEndTime.Value:yyyy/MM/dd HH:mm:ss}"
            : "End Time: --";

        foreach (var message in update.LogMessages)
        {
            Log.Append(message);
        }

        if (update.ExecutionRequest is not null)
        {
            var result = activityPerformer.Perform(update.ExecutionRequest);
            if (!result.Succeeded)
            {
                Log.Append($"[{update.ExecutionRequest.Timestamp:HH:mm:ss}] Action #{update.ExecutionRequest.ActionNumber} skipped - unable to get cursor position");
            }
            else
            {
                Log.Append($"[{update.ExecutionRequest.Timestamp:HH:mm:ss}] Action #{update.ExecutionRequest.ActionNumber} - Original position: X={result.OriginalX}, Y={result.OriginalY}, Offset=({update.ExecutionRequest.OffsetX},{update.ExecutionRequest.OffsetY})");
                Log.Append($"[{update.ExecutionRequest.Timestamp:HH:mm:ss}] Action #{update.ExecutionRequest.ActionNumber} completed - combo key {update.ExecutionRequest.KeyName} pressed and released");
            }
        }

        if (!update.IsRunning)
        {
            sessionTimer.Stop();
        }
    }

    private void NormalizeAllInputs()
    {
        var settings = CreateSettings();
        IntervalMin = settings.IntervalMinSeconds.ToString();
        IntervalMax = settings.IntervalMaxSeconds.ToString();
        IdleSeconds = settings.IdleThresholdSeconds.ToString();
        EndTimeText = settings.EndTimeText;
    }

    private ActivitySettings CreateSettings()
    {
        return ActivitySettings.FromInput(IntervalMin, IntervalMax, IdleSeconds, IsEndTimeEnabled, EndTimeText);
    }
}
