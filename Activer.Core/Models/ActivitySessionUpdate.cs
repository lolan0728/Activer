namespace Activer.Core.Models;

public sealed class ActivitySessionUpdate
{
    public ActivitySessionUpdate(
        bool isRunning,
        TimeSpan runTime,
        DateTime? targetEndTime,
        int actionCount,
        int nextIntervalSeconds,
        ActivityExecutionRequest? executionRequest,
        IReadOnlyList<string> logMessages)
    {
        IsRunning = isRunning;
        RunTime = runTime;
        TargetEndTime = targetEndTime;
        ActionCount = actionCount;
        NextIntervalSeconds = nextIntervalSeconds;
        ExecutionRequest = executionRequest;
        LogMessages = logMessages;
    }

    public bool IsRunning { get; }

    public TimeSpan RunTime { get; }

    public DateTime? TargetEndTime { get; }

    public int ActionCount { get; }

    public int NextIntervalSeconds { get; }

    public ActivityExecutionRequest? ExecutionRequest { get; }

    public IReadOnlyList<string> LogMessages { get; }
}
