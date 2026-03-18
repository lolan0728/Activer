using System.Windows.Threading;
using Activer.Core.Services;

namespace Activer.Services;

public sealed class DispatcherTimerFactory : ITimerFactory
{
    public ITimer CreateTimer()
    {
        return new DispatcherTimerAdapter();
    }
}

internal sealed class DispatcherTimerAdapter : ITimer
{
    private readonly DispatcherTimer timer = new();

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => timer.Interval;
        set => timer.Interval = value;
    }

    public void Dispose()
    {
        timer.Tick -= Timer_Tick;
        timer.Stop();
    }

    public void Start()
    {
        timer.Tick -= Timer_Tick;
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    public void Stop()
    {
        timer.Stop();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        Tick?.Invoke(this, e);
    }
}
