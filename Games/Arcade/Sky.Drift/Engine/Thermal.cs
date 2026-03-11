namespace SkyDrift.Engine;

public enum ThermalType { Rising, Downdraft }

/// <summary>열상승기류 / 하강기류</summary>
public class Thermal
{
    public double X      { get; set; }  // 화면 기준 X
    public double Y      { get; set; }  // 세계 좌표 Y (고도)
    public double Radius { get; set; }  // 영향 반경
    public double Strength { get; set; } // 최대 기류 속도 (픽셀/s)
    public ThermalType Type { get; set; }

    public bool IsActive(double altitude) => Math.Abs(altitude - Y) < Radius * 3;

    /// <summary>글라이더 X에 작용하는 수직 기류 속도 반환</summary>
    public double GetLift(double gliderX, double altitude)
    {
        double distX = Math.Abs(gliderX - X);
        double distY = Math.Abs(altitude - Y);
        double dist  = Math.Sqrt(distX * distX + distY * distY);

        if (dist > Radius) return 0.0;

        double factor = 1.0 - dist / Radius;
        double lift   = Strength * factor * factor;
        return Type == ThermalType.Rising ? lift : -lift;
    }
}
