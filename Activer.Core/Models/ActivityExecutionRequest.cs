namespace Activer.Core.Models;

public sealed class ActivityExecutionRequest
{
    public ActivityExecutionRequest(int actionNumber, DateTime timestamp, int offsetX, int offsetY)
    {
        ActionNumber = actionNumber;
        Timestamp = timestamp;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public int ActionNumber { get; }

    public DateTime Timestamp { get; }

    public int OffsetX { get; }

    public int OffsetY { get; }
}
