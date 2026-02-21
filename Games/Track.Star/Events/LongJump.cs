namespace TrackStar.Events;

/// <summary>
/// 멀리뛰기: ←→ 도움닫기 + SPACE 타이밍에 맞춰 점프!
/// </summary>
public sealed class LongJump : SportEvent
{
    private bool _lastWasLeft;
    private double _runSpeed;
    private bool _jumped;
    private double _jumpPower;
    private double _airTimer;
    private double _distance;
    private int _attempt;
    private double _bestDistance;
    private readonly Random _rng = new();

    public bool HasJumped => _jumped;
    public double JumpHeight => _jumped ? Math.Sin(_airTimer * Math.PI / 0.8) * 60 : 0;
    public int Attempt => _attempt;
    public double BestDistance => _bestDistance;

    public LongJump()
    {
        Name = "멀리뛰기";
        Instructions = "←→ 도움닫기 → SPACE 점프! (3회 시도)";
    }

    public override void Reset()
    {
        Phase = EventPhase.Ready;
        PlayerPos = 0;
        RivalPos = [0, 0, 0];
        _runSpeed = 0;
        _lastWasLeft = false;
        _jumped = false;
        _jumpPower = 0;
        _airTimer = 0;
        _distance = 0;
        _attempt = 1;
        _bestDistance = 0;
        Timer = 0;
    }

    public override void Update(double dt, bool leftKey, bool rightKey, bool spaceKey)
    {
        if (UpdateCountdown(dt)) return;

        if (Phase != EventPhase.Active) return;

        Timer += dt;

        if (!_jumped)
        {
            // 도움닫기
            _runSpeed *= 0.93;
            if (leftKey && !_lastWasLeft) { _runSpeed += 0.06; _lastWasLeft = true; }
            if (rightKey && _lastWasLeft) { _runSpeed += 0.06; _lastWasLeft = false; }
            _runSpeed = Math.Min(_runSpeed, 1.0);
            PlayerPos += _runSpeed * dt * 0.3;

            // 점프! (도약 라인 0.7 근처)
            if (spaceKey && PlayerPos >= 0.3)
            {
                _jumped = true;
                _jumpPower = _runSpeed;
                // 도약 라인(0.7)에 가까울수록 보너스
                double lineBonus = 1.0 - Math.Abs(PlayerPos - 0.7) * 2;
                lineBonus = Math.Max(0.3, lineBonus);
                _jumpPower *= lineBonus;
                _airTimer = 0;
            }

            // 파울 (도약 라인 넘기기)
            if (PlayerPos > 0.75 && !_jumped)
            {
                _jumped = true;
                _jumpPower = 0; // 파울
                _airTimer = 0;
            }
        }
        else
        {
            // 체공
            _airTimer += dt;
            _distance = _jumpPower * 8.5; // 미터 환산

            if (_airTimer >= 0.8)
            {
                // 착지
                if (_distance > _bestDistance) _bestDistance = _distance;

                if (_attempt >= 3)
                {
                    Phase = EventPhase.Result;
                    double rivalBest = 5.0 + _rng.NextDouble() * 3.0;
                    Result = _bestDistance;
                    string medal = _bestDistance >= rivalBest ? "🥇" : _bestDistance >= rivalBest - 0.5 ? "🥈" : "🥉";
                    ResultText = $"{medal} 최고 기록: {_bestDistance:F2}m";
                }
                else
                {
                    // 다음 시도
                    _attempt++;
                    PlayerPos = 0;
                    _runSpeed = 0;
                    _jumped = false;
                    _lastWasLeft = false;
                }
            }
        }
    }
}
