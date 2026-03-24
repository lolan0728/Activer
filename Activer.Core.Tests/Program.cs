using Activer.Core.Engine;
using Activer.Core.Models;
using Activer.Core.Services;

var tests = new (string Name, Action Run)[]
{
    ("Start initializes session and target end time", Start_InitializesSessionAndTargetEndTime),
    ("Stop ends session and returns runtime", Stop_EndsSessionAndReturnsFinalRuntime),
    ("Idle threshold gates the first action", Tick_DoesNotTriggerBeforeIdleThreshold),
    ("Idle and interval are both required", Tick_RequiresIdleAndIntervalBeforeTriggeringActivity),
    ("Zero mouse offset is normalized to a movement", Tick_NormalizesZeroMouseOffset),
    ("Target end time auto-stops the session", Tick_AutoStopsWhenTargetEndTimeIsReached),
    ("Settings normalization clamps and swaps values", ActivitySettings_NormalizesRangesAndInvalidValues),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL {test.Name}");
    }
}

if (failures.Count > 0)
{
    throw new InvalidOperationException("Tests failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
}

static void Start_InitializesSessionAndTargetEndTime()
{
    var clock = new FakeClock(new DateTime(2026, 3, 18, 17, 30, 0));
    var engine = new ActivitySessionEngine(clock, new FakeRandomSource(15));
    var settings = ActivitySettings.FromInput("10", "60", "60", true, "18:00:00");

    var update = engine.Start(settings);

    Assert(update.IsRunning, "Session should be running after start.");
    AssertEqual(0, update.ActionCount, "Action count should reset to zero.");
    AssertEqual(15, update.NextIntervalSeconds, "Next interval should come from the random source.");
    AssertEqual(new DateTime(2026, 3, 18, 18, 0, 0), update.TargetEndTime, "Target end time should be computed for today.");
    AssertSequence(
        new[]
        {
            "[17:30:00] Activity started",
            "[17:30:00] Next activity in 15 seconds",
        },
        update.LogMessages,
        "Start log messages do not match.");
}

static void Stop_EndsSessionAndReturnsFinalRuntime()
{
    var clock = new FakeClock(new DateTime(2026, 3, 18, 10, 0, 0));
    var engine = new ActivitySessionEngine(clock, new FakeRandomSource(10));
    engine.Start(ActivitySettings.FromInput("10", "60", "60", false, "18:00:00"));

    clock.Advance(TimeSpan.FromMinutes(5));
    var update = engine.Stop();

    Assert(!update.IsRunning, "Session should stop.");
    AssertEqual(TimeSpan.FromMinutes(5), update.RunTime, "Stop should report final runtime.");
    AssertSequence(
        new[] { "[10:05:00] Activity stopped, total run time: 00:05:00" },
        update.LogMessages,
        "Stop log messages do not match.");
}

static void Tick_DoesNotTriggerBeforeIdleThreshold()
{
    var clock = new FakeClock(new DateTime(2026, 3, 18, 10, 0, 0));
    var engine = new ActivitySessionEngine(clock, new FakeRandomSource(10, 3, 4, 10));
    engine.Start(ActivitySettings.FromInput("10", "10", "60", false, "18:00:00"));

    clock.Advance(TimeSpan.FromSeconds(59));
    var beforeThreshold = engine.Tick(59);

    clock.Advance(TimeSpan.FromSeconds(1));
    var atThreshold = engine.Tick(60);

    Assert(beforeThreshold.ExecutionRequest is null, "Activity should not trigger before the idle threshold.");
    AssertEqual(0, beforeThreshold.ActionCount, "Action count should stay at zero before the threshold.");
    Assert(atThreshold.ExecutionRequest is not null, "Activity should trigger once the idle threshold and interval are satisfied.");
    AssertEqual(1, atThreshold.ActionCount, "Action count should increment when the activity runs.");
}

static void Tick_RequiresIdleAndIntervalBeforeTriggeringActivity()
{
    var clock = new FakeClock(new DateTime(2026, 3, 18, 10, 0, 0));
    var engine = new ActivitySessionEngine(clock, new FakeRandomSource(30, 3, 4, 30));
    engine.Start(ActivitySettings.FromInput("30", "30", "10", false, "18:00:00"));

    clock.Advance(TimeSpan.FromSeconds(10));
    var thresholdOnly = engine.Tick(10);

    clock.Advance(TimeSpan.FromSeconds(19));
    var almostReady = engine.Tick(29);

    clock.Advance(TimeSpan.FromSeconds(1));
    var ready = engine.Tick(30);

    Assert(thresholdOnly.ExecutionRequest is null, "Idle threshold alone should not trigger the action.");
    Assert(almostReady.ExecutionRequest is null, "The action should still wait for the full interval.");
    Assert(ready.ExecutionRequest is not null, "The action should trigger once both conditions are met.");
    AssertEqual(1, ready.ActionCount, "Action count should increment when the request is created.");
    AssertEqual(3, ready.ExecutionRequest!.OffsetX, "OffsetX should come from the random source.");
    AssertEqual(4, ready.ExecutionRequest.OffsetY, "OffsetY should come from the random source.");
    AssertEqual(30, ready.NextIntervalSeconds, "A new interval should be scheduled after the action.");
}

static void Tick_NormalizesZeroMouseOffset()
{
    var clock = new FakeClock(new DateTime(2026, 3, 18, 10, 0, 0));
    var engine = new ActivitySessionEngine(clock, new FakeRandomSource(10, 0, 0, 10));
    engine.Start(ActivitySettings.FromInput("10", "10", "10", false, "18:00:00"));

    clock.Advance(TimeSpan.FromSeconds(10));
    var update = engine.Tick(10);

    Assert(update.ExecutionRequest is not null, "The action should trigger once idle and interval are satisfied.");
    AssertEqual(1, update.ExecutionRequest!.OffsetX, "Zero offset should normalize to a non-zero X movement.");
    AssertEqual(0, update.ExecutionRequest.OffsetY, "Zero offset normalization should preserve the Y axis.");
}

static void Tick_AutoStopsWhenTargetEndTimeIsReached()
{
    var clock = new FakeClock(new DateTime(2026, 3, 18, 17, 59, 50));
    var engine = new ActivitySessionEngine(clock, new FakeRandomSource(10));
    engine.Start(ActivitySettings.FromInput("10", "10", "5", true, "18:00:00"));

    clock.Advance(TimeSpan.FromSeconds(10));
    var update = engine.Tick(100);

    Assert(!update.IsRunning, "Session should stop once the target end time is reached.");
    AssertEqual(TimeSpan.FromSeconds(10), update.RunTime, "Auto-stop should preserve runtime.");
    AssertEqual<DateTime?>(null, update.TargetEndTime, "Target end time should be cleared after stopping.");
    AssertSequence(
        new[]
        {
            "[18:00:00] Reached end time (2026/03/18 18:00:00), stopping activity automatically.",
            "[18:00:00] Activity stopped, total run time: 00:00:10",
        },
        update.LogMessages,
        "Auto-stop log messages do not match.");
}

static void ActivitySettings_NormalizesRangesAndInvalidValues()
{
    var settings = ActivitySettings.FromInput("1000", "0", "x", true, "28:90:71");

    AssertEqual(1, settings.IntervalMinSeconds, "Interval min should clamp and swap.");
    AssertEqual(999, settings.IntervalMaxSeconds, "Interval max should clamp and swap.");
    AssertEqual(ActivitySettings.DefaultIdleSeconds, settings.IdleThresholdSeconds, "Idle threshold should fall back to the default.");
    AssertEqual("23:59:59", settings.EndTimeText, "End time should clamp invalid values.");
    AssertEqual(new TimeSpan(23, 59, 59), settings.EndTimeOfDay, "End time of day should match the normalized text.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}.");
    }
}

static void AssertSequence(IEnumerable<string> expected, IEnumerable<string> actual, string message)
{
    var expectedArray = expected.ToArray();
    var actualArray = actual.ToArray();

    if (!expectedArray.SequenceEqual(actualArray))
    {
        throw new InvalidOperationException(
            $"{message} Expected: [{string.Join(" | ", expectedArray)}]; Actual: [{string.Join(" | ", actualArray)}].");
    }
}

sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; private set; }

    public void Advance(TimeSpan delta)
    {
        Now = Now.Add(delta);
    }
}

sealed class FakeRandomSource : IRandomSource
{
    private readonly Queue<int> values;

    public FakeRandomSource(params int[] values)
    {
        this.values = new Queue<int>(values);
    }

    public int Next(int minValue, int maxValueExclusive)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("No fake random values remain.");
        }

        var next = values.Dequeue();
        if (next < minValue || next >= maxValueExclusive)
        {
            throw new InvalidOperationException($"Fake value {next} is outside [{minValue}, {maxValueExclusive}).");
        }

        return next;
    }
}
