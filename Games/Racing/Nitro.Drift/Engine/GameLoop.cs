using System.Diagnostics;
using System.Windows.Media;

namespace NitroDrift.Engine;

public sealed class GameLoop
{
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastFrame;
    private bool _running;
    public double DeltaTime { get; private set; }
    public event Action<double>? OnUpdate;
    public event Action? OnRender;

    public void Start() { if (_running) return; _running = true; _clock.Restart(); _lastFrame = TimeSpan.Zero; CompositionTarget.Rendering += Tick; }
    public void Stop() { _running = false; _clock.Stop(); CompositionTarget.Rendering -= Tick; }
    public void Pause() { _running = false; _clock.Stop(); CompositionTarget.Rendering -= Tick; }
    public void Resume() { _running = true; _clock.Start(); CompositionTarget.Rendering += Tick; }

    private void Tick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        DeltaTime = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (DeltaTime > 0.1) DeltaTime = 0.1;
        OnUpdate?.Invoke(DeltaTime);
        OnRender?.Invoke();
    }
}
