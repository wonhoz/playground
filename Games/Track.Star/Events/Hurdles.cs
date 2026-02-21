namespace TrackStar.Events;

/// <summary>
/// 110m 허들: ←→ 달리기 + SPACE 점프로 허들 넘기!
/// </summary>
public sealed class Hurdles : SportEvent
{
    private bool _lastWasLeft;
    private double _speed;
    private bool _jumping;
    private double _jumpTimer;
    private int _hurdlesPassed;
    private int _hurdlesHit;
    private readonly Random _rng = new();
    private readonly double[] _rivalSpeeds = new double[3];

    // 허들 위치 (0~1)
    private readonly double[] _hurdlePositions = [0.15, 0.3, 0.45, 0.6, 0.75, 0.88];

    public bool IsJumping => _jumping;
    public double[] HurdlePositions => _hurdlePositions;
    public int HurdlesHit => _hurdlesHit;

    public Hurdles()
    {
        Name = "110m 허들";
        Instructions = "←→ 달리기 + SPACE 점프!";
    }

    public override void Reset()
    {
        Phase = EventPhase.Ready;
        PlayerPos = 0;
        RivalPos = [0, 0, 0];
        _speed = 0;
        _lastWasLeft = false;
        _jumping = false;
        _jumpTimer = 0;
        _hurdlesPassed = 0;
        _hurdlesHit = 0;
        Timer = 0;
        for (int i = 0; i < 3; i++)
            _rivalSpeeds[i] = 0.24 + _rng.NextDouble() * 0.10;
    }

    public override void Update(double dt, bool leftKey, bool rightKey, bool spaceKey)
    {
        if (UpdateCountdown(dt)) return;

        if (Phase == EventPhase.Active)
        {
            Timer += dt;
            _speed *= 0.92;

            if (leftKey && !_lastWasLeft) { _speed += 0.07; _lastWasLeft = true; }
            if (rightKey && _lastWasLeft) { _speed += 0.07; _lastWasLeft = false; }

            // 점프
            if (spaceKey && !_jumping)
            {
                _jumping = true;
                _jumpTimer = 0.4;
            }
            if (_jumping)
            {
                _jumpTimer -= dt;
                if (_jumpTimer <= 0) _jumping = false;
            }

            _speed = Math.Min(_speed, 0.9);
            PlayerPos += _speed * dt * 0.45;

            // 허들 충돌
            foreach (var hp in _hurdlePositions)
            {
                if (Math.Abs(PlayerPos - hp) < 0.02 && _hurdlesPassed < _hurdlePositions.Length)
                {
                    if (!_jumping)
                    {
                        _hurdlesHit++;
                        _speed *= 0.5; // 감속 패널티
                    }
                    _hurdlesPassed++;
                }
            }

            for (int i = 0; i < 3; i++)
                RivalPos[i] += _rivalSpeeds[i] * dt * 0.45;

            if (PlayerPos >= 1.0 || RivalPos.Any(r => r >= 1.0))
            {
                Phase = EventPhase.Result;
                Result = Timer;
                int rank = 1;
                foreach (var r in RivalPos) if (r >= PlayerPos) rank++;

                string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => "💀" };
                ResultText = $"{medal} {rank}등! 기록: {Timer:F2}초 (넘어진 허들: {_hurdlesHit})";
            }
        }
    }
}
