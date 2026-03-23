namespace TrackStar.Events;

/// <summary>
/// 스포츠 종목 베이스.
/// </summary>
public enum EventPhase { Ready, Countdown, Active, Result }

public abstract class SportEvent
{
    public string Name { get; protected set; } = "";
    public string Instructions { get; protected set; } = "";
    public EventPhase Phase { get; set; } = EventPhase.Ready;
    public double Timer { get; set; }
    public double Result { get; set; }
    public string ResultText { get; set; } = "";
    public bool IsComplete => Phase == EventPhase.Result;

    // 러너 위치 (0~1 정규화)
    public double PlayerPos { get; set; }
    public double[] RivalPos { get; set; } = [0, 0, 0];

    public abstract void Update(double dt, bool leftKey, bool rightKey, bool spaceKey);
    public abstract void Reset();

    protected double CountdownTimer;
    protected const double CountdownDuration = 3.0;

    protected bool UpdateCountdown(double dt)
    {
        if (Phase == EventPhase.Ready)
        {
            Phase = EventPhase.Countdown;
            CountdownTimer = CountdownDuration;
        }

        if (Phase == EventPhase.Countdown)
        {
            CountdownTimer -= dt;
            Timer = CountdownTimer;
            if (CountdownTimer <= 0)
            {
                Phase = EventPhase.Active;
                Timer = 0;
            }
            return true;
        }
        return false;
    }
}
