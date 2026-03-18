using Activer.Core.Models;
using Activer.Core.Services;

namespace Activer.Core.Engine;

public sealed class ActivitySessionEngine
{
    private static readonly (byte KeyCode, string KeyName)[] ComboKeys =
    {
        (0x10, "Shift"),
        (0x11, "Ctrl"),
        (0x12, "Alt"),
    };

    private readonly IClock clock;
    private readonly IRandomSource randomSource;

    private ActivitySettings? settings;
    private DateTime sessionStartTime;
    private DateTime eligibilityAnchorTime;
    private DateTime? targetEndTime;
    private int nextIntervalSeconds;
    private int actionCount;
    private bool isBelowIdleThreshold = true;

    public ActivitySessionEngine(IClock clock, IRandomSource randomSource)
    {
        this.clock = clock;
        this.randomSource = randomSource;
    }

    public bool IsRunning { get; private set; }

    public ActivitySessionUpdate Start(ActivitySettings nextSettings)
    {
        settings = nextSettings;
        sessionStartTime = clock.Now;
        eligibilityAnchorTime = sessionStartTime;
        targetEndTime = ComputeTargetEndTime(sessionStartTime, settings);
        nextIntervalSeconds = randomSource.Next(settings.IntervalMinSeconds, settings.IntervalMaxSeconds + 1);
        actionCount = 0;
        isBelowIdleThreshold = true;
        IsRunning = true;

        return new ActivitySessionUpdate(
            isRunning: true,
            runTime: TimeSpan.Zero,
            targetEndTime: targetEndTime,
            actionCount: actionCount,
            nextIntervalSeconds: nextIntervalSeconds,
            executionRequest: null,
            logMessages: new[]
            {
                $"[{sessionStartTime:HH:mm:ss}] Activity started",
                $"[{sessionStartTime:HH:mm:ss}] Next activity in {nextIntervalSeconds} seconds",
            });
    }

    public ActivitySessionUpdate Tick(int idleSeconds)
    {
        if (!IsRunning || settings is null)
        {
            return new ActivitySessionUpdate(false, TimeSpan.Zero, null, actionCount, 0, null, Array.Empty<string>());
        }

        var now = clock.Now;
        var runTime = now - sessionStartTime;
        var logMessages = new List<string>();

        if (targetEndTime.HasValue && now >= targetEndTime.Value)
        {
            IsRunning = false;
            logMessages.Add($"[{now:HH:mm:ss}] Reached end time ({targetEndTime.Value:yyyy/MM/dd HH:mm:ss}), stopping activity automatically.");
            logMessages.Add($"[{now:HH:mm:ss}] Activity stopped, total run time: {runTime:hh\\:mm\\:ss}");

            return new ActivitySessionUpdate(false, runTime, null, actionCount, nextIntervalSeconds, null, logMessages);
        }

        if (idleSeconds < settings.IdleThresholdSeconds)
        {
            if (!isBelowIdleThreshold)
            {
                logMessages.Add($"[{now:HH:mm:ss}] User activity detected, resetting idle timer");
            }

            eligibilityAnchorTime = now.AddSeconds(-idleSeconds);
            isBelowIdleThreshold = true;

            return new ActivitySessionUpdate(true, runTime, targetEndTime, actionCount, nextIntervalSeconds, null, logMessages);
        }

        isBelowIdleThreshold = false;

        if ((now - eligibilityAnchorTime).TotalSeconds < nextIntervalSeconds)
        {
            return new ActivitySessionUpdate(true, runTime, targetEndTime, actionCount, nextIntervalSeconds, null, logMessages);
        }

        actionCount++;
        var offsetX = randomSource.Next(-10, 11);
        var offsetY = randomSource.Next(-10, 11);
        var comboKey = ComboKeys[randomSource.Next(0, ComboKeys.Length)];

        var executionRequest = new ActivityExecutionRequest(actionCount, now, offsetX, offsetY, comboKey.KeyCode, comboKey.KeyName);

        eligibilityAnchorTime = now;
        nextIntervalSeconds = randomSource.Next(settings.IntervalMinSeconds, settings.IntervalMaxSeconds + 1);
        logMessages.Add($"[{now:HH:mm:ss}] Next activity in {nextIntervalSeconds} seconds");

        return new ActivitySessionUpdate(true, runTime, targetEndTime, actionCount, nextIntervalSeconds, executionRequest, logMessages);
    }

    public ActivitySessionUpdate Stop()
    {
        if (!IsRunning)
        {
            return new ActivitySessionUpdate(false, TimeSpan.Zero, null, actionCount, 0, null, Array.Empty<string>());
        }

        var now = clock.Now;
        var runTime = now - sessionStartTime;
        IsRunning = false;

        return new ActivitySessionUpdate(
            false,
            runTime,
            null,
            actionCount,
            nextIntervalSeconds,
            null,
            new[] { $"[{now:HH:mm:ss}] Activity stopped, total run time: {runTime:hh\\:mm\\:ss}" });
    }

    private static DateTime? ComputeTargetEndTime(DateTime now, ActivitySettings settings)
    {
        if (!settings.IsEndTimeEnabled || !settings.EndTimeOfDay.HasValue)
        {
            return null;
        }

        var todayEndTime = now.Date.Add(settings.EndTimeOfDay.Value);
        return now >= todayEndTime ? todayEndTime.AddDays(1) : todayEndTime;
    }
}
