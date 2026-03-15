namespace WaveSurf.Engine;

/// <summary>다중 사인파 합성으로 자연스러운 파도 생성</summary>
public class WavePhysics
{
    // 1차 파도
    private const double K1 = 0.011;
    private const double W1 = 0.9;
    // 2차 파도 (불규칙성 추가)
    private const double K2 = 0.019;
    private const double W2 = 1.2;
    private const double Ph2 = 0.8;
    // 3차 파도 (긴 너울)
    private const double K3 = 0.006;
    private const double W3 = 0.55;
    private const double Ph3 = 1.7;
    // 4차 파도 (잔물결)
    private const double K4 = 0.031;
    private const double W4 = 2.1;
    private const double Ph4 = 3.1;

    private double _time;
    private double _scrollOffset;
    private double _sessionTime;

    public double CanvasWidth  { get; set; } = 1200;
    public double CanvasHeight { get; set; } = 680;

    /// <summary>평균 수면 Y 좌표 (캔버스 좌표 — 위가 0)</summary>
    public double BaselineY => CanvasHeight * 0.50;

    /// <summary>현재 파도 진폭 (세션 진행에 따라 증가)</summary>
    public double Amplitude { get; private set; } = 55;

    /// <summary>스크롤 오프셋 (월드 X 좌표 기반)</summary>
    public double ScrollOffset => _scrollOffset;

    public double Time => _time;

    public void Update(double dt)
    {
        _time += dt;
        _sessionTime += dt;
        // 초당 100px 스크롤 (서핑 속도감)
        _scrollOffset += 100.0 * dt;
        // 0~300초 동안 진폭 55 → 130 증가 (3~5분)
        double progress = Math.Clamp(_sessionTime / 300.0, 0, 1);
        Amplitude = 55 + 75 * progress;
    }

    /// <summary>스크린 X에서 파도 표면의 캔버스 Y 반환</summary>
    public double SurfaceY(double screenX)
    {
        double wx = screenX + _scrollOffset;
        double h = Amplitude * (
            0.55 * Math.Sin(K1 * wx - W1 * _time) +
            0.25 * Math.Sin(K2 * wx - W2 * _time + Ph2) +
            0.15 * Math.Sin(K3 * wx - W3 * _time + Ph3) +
            0.05 * Math.Sin(K4 * wx - W4 * _time + Ph4)
        );
        // 캔버스는 Y가 아래로 증가 → 높이가 클수록 Y가 작아짐
        return BaselineY - h;
    }

    /// <summary>파도 기울기 (dY/dX) — 서퍼 각도 계산에 사용</summary>
    public double Slope(double screenX)
        => (SurfaceY(screenX + 2) - SurfaceY(screenX - 2)) / 4.0;

    /// <summary>파도 표면의 수직 속도 (dY/dt) — 음수 = 위로 상승 중</summary>
    public double SurfaceVelocityY(double screenX)
    {
        const double eps = 0.008;
        _time += eps;
        double y1 = SurfaceY(screenX);
        _time -= 2 * eps;
        double y0 = SurfaceY(screenX);
        _time += eps;
        return (y1 - y0) / (2 * eps);
    }

    public void Reset()
    {
        _time = 0;
        _scrollOffset = 0;
        _sessionTime = 0;
        Amplitude = 55;
    }
}
