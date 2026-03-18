using Activer.Core.Services;

namespace Activer.Services;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
