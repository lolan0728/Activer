namespace Activer.Core.Models;

public sealed class ActivityExecutionResult
{
    public ActivityExecutionResult(bool succeeded, int originalX, int originalY)
    {
        Succeeded = succeeded;
        OriginalX = originalX;
        OriginalY = originalY;
    }

    public bool Succeeded { get; }

    public int OriginalX { get; }

    public int OriginalY { get; }
}
