namespace SkyDrift.Engine;

/// <summary>글라이더 날개 양력·중력 물리</summary>
public class GliderPhysics
{
    // 물리 상수
    private const double Gravity    = 80.0;    // 중력 가속도 (픽셀/s²)
    private const double LiftCoeff  = 180.0;   // 양력 계수
    private const double DragCoeff  = 0.12;    // 공기저항
    private const double MaxTilt    = 35.0;    // 최대 기울기 각도 (도)
    private const double TiltSpeed  = 80.0;    // 기울기 변환 속도
    private const double MinSpeed   = 60.0;    // 최소 수평 속도
    private const double MaxSpeed   = 300.0;   // 최대 수평 속도

    // 상태
    public double X       { get; private set; }   // 수평 위치
    public double VX      { get; private set; } = 80.0;  // 수평 속도
    public double VY      { get; private set; }   // 수직 속도 (양수=상승)
    public double Tilt    { get; private set; }   // 기울기 (도, 양수=우회전)
    public bool   IsAlive { get; private set; } = true;

    private double _targetTilt;

    public void Init(double startX)
    {
        X       = startX;
        VX      = 80.0;
        VY      = 0.0;
        Tilt    = 0.0;
        IsAlive = true;
    }

    public void TiltLeft()  => _targetTilt = -MaxTilt;
    public void TiltRight() => _targetTilt =  MaxTilt;
    public void ReleaseTilt() => _targetTilt = 0.0;

    /// <summary>vLift: 현재 위치의 열상승기류 속도 (양수=상승)</summary>
    public void Update(double dt, double vLift)
    {
        // 기울기 보간
        double diff = _targetTilt - Tilt;
        Tilt += Math.Sign(diff) * Math.Min(Math.Abs(diff), TiltSpeed * dt);

        // 양력: 수평 속도에 비례 + 기울기에 따라 수평 이동
        double tiltRad = Tilt * Math.PI / 180.0;
        double lift    = LiftCoeff * (VX / 100.0) * Math.Cos(tiltRad);

        // 수직 속도
        double netVY = lift - Gravity + vLift;
        VY += (netVY - VY) * (1.0 - Math.Exp(-3.0 * dt));  // 스무딩

        // 수평 이동 (기울기에 따라)
        VX = Math.Clamp(VX - DragCoeff * VX * dt + Math.Sin(tiltRad) * 40 * dt, MinSpeed, MaxSpeed);

        X += Math.Sin(tiltRad) * VX * dt * 0.4;
    }

    public void Kill() => IsAlive = false;
}
