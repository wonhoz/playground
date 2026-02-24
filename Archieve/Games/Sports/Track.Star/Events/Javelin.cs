namespace TrackStar.Events;

/// <summary>
/// 창던지기: ←→ 도움닫기 + SPACE 파워 게이지 + SPACE 각도!
/// </summary>
public sealed class Javelin : SportEvent
{
    private bool _lastWasLeft;
    private double _runSpeed;

    public enum JavelinPhase { Run, Power, Angle, Flight, Land }
    private JavelinPhase _jPhase = JavelinPhase.Run;

    private double _powerGauge; // 0~1 왕복
    private double _powerDir = 1;
    private double _power;
    private double _angleGauge;
    private double _angleDir = 1;
    private double _angle; // 도
    private double _flightTimer;
    private double _distance;

    private int _attempt;
    private double _bestDistance;
    private readonly Random _rng = new();

    public double PowerGauge => _powerGauge;
    public double AngleGauge => _angleGauge;
    public JavelinPhase CurrentJPhase => _jPhase;
    public bool InPowerPhase => _jPhase == JavelinPhase.Power;
    public bool InAnglePhase => _jPhase == JavelinPhase.Angle;
    public bool InFlight => _jPhase == JavelinPhase.Flight;
    public double FlightProgress => _flightTimer / 1.2;
    public int Attempt => _attempt;
    public double BestDistance => _bestDistance;

    public Javelin()
    {
        Name = "창던지기";
        Instructions = "←→ 달리기 → SPACE 파워 → SPACE 각도!";
    }

    public override void Reset()
    {
        Phase = EventPhase.Ready;
        PlayerPos = 0;
        RivalPos = [0, 0, 0];
        _runSpeed = 0;
        _lastWasLeft = false;
        _jPhase = JavelinPhase.Run;
        _powerGauge = 0;
        _powerDir = 1;
        _power = 0;
        _angleGauge = 0.5;
        _angleDir = 1;
        _angle = 0;
        _flightTimer = 0;
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

        switch (_jPhase)
        {
            case JavelinPhase.Run:
                _runSpeed *= 0.93;
                if (leftKey && !_lastWasLeft) { _runSpeed += 0.06; _lastWasLeft = true; }
                if (rightKey && _lastWasLeft) { _runSpeed += 0.06; _lastWasLeft = false; }
                _runSpeed = Math.Min(_runSpeed, 1.0);
                PlayerPos += _runSpeed * dt * 0.4;

                if (PlayerPos >= 0.6)
                {
                    _jPhase = JavelinPhase.Power;
                    _powerGauge = 0;
                }
                break;

            case JavelinPhase.Power:
                _powerGauge += _powerDir * dt * 2.5;
                if (_powerGauge >= 1) { _powerGauge = 1; _powerDir = -1; }
                if (_powerGauge <= 0) { _powerGauge = 0; _powerDir = 1; }

                if (spaceKey)
                {
                    _power = _powerGauge;
                    _jPhase = JavelinPhase.Angle;
                    _angleGauge = 0.5;
                }
                break;

            case JavelinPhase.Angle:
                _angleGauge += _angleDir * dt * 2.0;
                if (_angleGauge >= 1) { _angleGauge = 1; _angleDir = -1; }
                if (_angleGauge <= 0) { _angleGauge = 0; _angleDir = 1; }

                if (spaceKey)
                {
                    _angle = _angleGauge * 80 + 10; // 10~90도
                    // 최적 각도 = 약 40~45도
                    double angleBonus = 1.0 - Math.Abs(_angle - 42) / 50.0;
                    angleBonus = Math.Max(0.3, angleBonus);
                    _distance = _runSpeed * _power * angleBonus * 90;
                    _jPhase = JavelinPhase.Flight;
                    _flightTimer = 0;
                }
                break;

            case JavelinPhase.Flight:
                _flightTimer += dt;
                if (_flightTimer >= 1.2)
                {
                    _jPhase = JavelinPhase.Land;
                    if (_distance > _bestDistance) _bestDistance = _distance;

                    if (_attempt >= 3)
                    {
                        Phase = EventPhase.Result;
                        double rivalBest = 40 + _rng.NextDouble() * 30;
                        Result = _bestDistance;
                        string medal = _bestDistance >= rivalBest ? "🥇" : _bestDistance >= rivalBest - 5 ? "🥈" : "🥉";
                        ResultText = $"{medal} 최고 기록: {_bestDistance:F1}m";
                    }
                    else
                    {
                        _attempt++;
                        PlayerPos = 0;
                        _runSpeed = 0;
                        _lastWasLeft = false;
                        _jPhase = JavelinPhase.Run;
                    }
                }
                break;
        }
    }
}
