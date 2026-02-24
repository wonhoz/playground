namespace TrackStar.Events;

/// <summary>
/// 100m 단거리: ←→ 키를 빠르게 번갈아 눌러서 달리기!
/// </summary>
public sealed class Sprint100m : SportEvent
{
    private bool _lastWasLeft;
    private double _speed;
    private readonly Random _rng = new();
    private readonly double[] _rivalSpeeds = new double[3];

    public Sprint100m()
    {
        Name = "100m 단거리";
        Instructions = "←→ 키를 번갈아 빠르게 눌러라!";
    }

    public override void Reset()
    {
        Phase = EventPhase.Ready;
        PlayerPos = 0;
        RivalPos = [0, 0, 0];
        _speed = 0;
        _lastWasLeft = false;
        Timer = 0;

        for (int i = 0; i < 3; i++)
            _rivalSpeeds[i] = 0.28 + _rng.NextDouble() * 0.12;
    }

    public override void Update(double dt, bool leftKey, bool rightKey, bool spaceKey)
    {
        if (UpdateCountdown(dt)) return;

        if (Phase == EventPhase.Active)
        {
            Timer += dt;

            // 감속
            _speed *= 0.92;

            // 번갈아 누르기 감지
            if (leftKey && !_lastWasLeft) { _speed += 0.08; _lastWasLeft = true; }
            if (rightKey && _lastWasLeft) { _speed += 0.08; _lastWasLeft = false; }

            _speed = Math.Min(_speed, 1.0);
            PlayerPos += _speed * dt * 0.5;

            // 라이벌
            for (int i = 0; i < 3; i++)
                RivalPos[i] += _rivalSpeeds[i] * dt * 0.5;

            // 결과
            if (PlayerPos >= 1.0 || RivalPos.Any(r => r >= 1.0))
            {
                Phase = EventPhase.Result;
                Result = Timer;

                int rank = 1;
                foreach (var r in RivalPos)
                    if (r >= PlayerPos) rank++;

                ResultText = rank switch
                {
                    1 => $"🥇 1등! 기록: {Timer:F2}초",
                    2 => $"🥈 2등! 기록: {Timer:F2}초",
                    3 => $"🥉 3등! 기록: {Timer:F2}초",
                    _ => $"4등... 기록: {Timer:F2}초"
                };
            }
        }
    }
}
