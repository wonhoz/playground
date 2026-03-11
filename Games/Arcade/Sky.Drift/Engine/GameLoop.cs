namespace SkyDrift.Engine;

/// <summary>고정 타임스텝 게임 루프 (Neon.Run 패턴 재사용)</summary>
public sealed class GameLoop
{
    public event Action<double>? OnUpdate;
    public event Action?         OnRender;

    private System.Windows.Threading.DispatcherTimer? _timer;
    private DateTime _lastTime;
    private bool     _running;

    public void Start()
    {
        if (_running) return;
        _running  = true;
        _lastTime = DateTime.UtcNow;

        _timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Render)
        { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += Tick;
        _timer.Start();
    }

    public void Stop()
    {
        _running = false;
        _timer?.Stop();
    }

    private void Tick(object? s, EventArgs e)
    {
        if (!_running) return;
        var now = DateTime.UtcNow;
        double dt = Math.Min((now - _lastTime).TotalSeconds, 0.05);
        _lastTime = now;

        OnUpdate?.Invoke(dt);
        OnRender?.Invoke();
    }
}
