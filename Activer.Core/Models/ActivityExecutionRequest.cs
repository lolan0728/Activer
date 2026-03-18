namespace Activer.Core.Models;

public sealed class ActivityExecutionRequest
{
    public ActivityExecutionRequest(int actionNumber, DateTime timestamp, int offsetX, int offsetY, byte virtualKeyCode, string keyName)
    {
        ActionNumber = actionNumber;
        Timestamp = timestamp;
        OffsetX = offsetX;
        OffsetY = offsetY;
        VirtualKeyCode = virtualKeyCode;
        KeyName = keyName;
    }

    public int ActionNumber { get; }

    public DateTime Timestamp { get; }

    public int OffsetX { get; }

    public int OffsetY { get; }

    public byte VirtualKeyCode { get; }

    public string KeyName { get; }
}
