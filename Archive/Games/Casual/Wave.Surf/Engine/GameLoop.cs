using System.Windows.Threading;

namespace WaveSurf.Engine;

/// <summary>16 ms DispatcherTimer 기반 게임 루프</summary>
public sealed class GameLoop
{
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;

    public event Action<double>? OnTick; // dt (seconds)

    public bool IsRunning => _timer.IsEnabled;

    public GameLoop()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += Tick;
    }

    public void Start()
    {
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.001, 0.05);
        _lastTick = now;
        OnTick?.Invoke(dt);
    }
}
