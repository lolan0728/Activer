namespace Activer.Core.Services;

public interface ITimer : IDisposable
{
    event EventHandler? Tick;

    TimeSpan Interval { get; set; }

    void Start();

    void Stop();
}
