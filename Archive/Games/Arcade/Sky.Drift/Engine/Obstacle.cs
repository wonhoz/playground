namespace SkyDrift.Engine;

public enum ObstacleKind { BirdFlock, StormCloud, WindZone }

/// <summary>장애물 (새떼, 폭풍구름, 강풍 구역)</summary>
public class Obstacle
{
    public double X       { get; set; }
    public double Y       { get; set; }   // 세계 좌표 고도
    public double Width   { get; set; }
    public double Height  { get; set; }
    public ObstacleKind Kind { get; set; }
    public bool   Passed  { get; set; }

    // BirdFlock: 이동 방향
    public double VX { get; set; }  // 수평 이동 속도

    public bool HitTest(double gx, double gy, double hitRadius = 18)
    {
        return Math.Abs(gx - X) < Width / 2 + hitRadius &&
               Math.Abs(gy - Y) < Height / 2 + hitRadius;
    }
}
