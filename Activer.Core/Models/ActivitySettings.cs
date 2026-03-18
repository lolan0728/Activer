namespace Activer.Core.Models;

public sealed class ActivitySettings
{
    public const int DefaultIntervalMinSeconds = 10;
    public const int DefaultIntervalMaxSeconds = 60;
    public const int DefaultIdleSeconds = 60;
    public const string DefaultEndTimeText = "18:00:00";

    public ActivitySettings(
        int intervalMinSeconds,
        int intervalMaxSeconds,
        int idleThresholdSeconds,
        bool isEndTimeEnabled,
        string endTimeText,
        TimeSpan? endTimeOfDay)
    {
        IntervalMinSeconds = intervalMinSeconds;
        IntervalMaxSeconds = intervalMaxSeconds;
        IdleThresholdSeconds = idleThresholdSeconds;
        IsEndTimeEnabled = isEndTimeEnabled;
        EndTimeText = endTimeText;
        EndTimeOfDay = endTimeOfDay;
    }

    public int IntervalMinSeconds { get; }

    public int IntervalMaxSeconds { get; }

    public int IdleThresholdSeconds { get; }

    public bool IsEndTimeEnabled { get; }

    public string EndTimeText { get; }

    public TimeSpan? EndTimeOfDay { get; }

    public static ActivitySettings FromInput(
        string? intervalMinText,
        string? intervalMaxText,
        string? idleSecondsText,
        bool isEndTimeEnabled,
        string? endTimeText)
    {
        var min = NormalizePositiveInt(intervalMinText, DefaultIntervalMinSeconds);
        var max = NormalizePositiveInt(intervalMaxText, DefaultIntervalMaxSeconds);
        if (min > max)
        {
            (min, max) = (max, min);
        }

        var idle = NormalizePositiveInt(idleSecondsText, DefaultIdleSeconds);
        var normalizedTime = NormalizeTimeText(endTimeText);
        var endTimeOfDay = ParseTimeOfDay(normalizedTime);

        return new ActivitySettings(min, max, idle, isEndTimeEnabled, normalizedTime, endTimeOfDay);
    }

    public static int NormalizePositiveInt(string? text, int defaultValue, int min = 1, int max = 999)
    {
        if (!int.TryParse(text, out var value))
        {
            value = defaultValue;
        }

        return Math.Clamp(value, min, max);
    }

    public static string NormalizeTimeText(string? text)
    {
        var parts = (text ?? string.Empty).Split(':');
        if (parts.Length != 3)
        {
            return "00:00:00";
        }

        var hours = ParsePart(parts[0], 0, 23);
        var minutes = ParsePart(parts[1], 0, 59);
        var seconds = ParsePart(parts[2], 0, 59);

        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private static TimeSpan ParseTimeOfDay(string value)
    {
        var parts = value.Split(':');
        return new TimeSpan(
            ParsePart(parts[0], 0, 23),
            ParsePart(parts[1], 0, 59),
            ParsePart(parts[2], 0, 59));
    }

    private static int ParsePart(string? text, int min, int max)
    {
        if (!int.TryParse(text, out var value))
        {
            value = min;
        }

        return Math.Clamp(value, min, max);
    }
}
