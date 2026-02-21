namespace NitroDrift.Engine;

/// <summary>
/// 서킷 트랙 정의. 웨이포인트 기반 코스.
/// </summary>
public sealed class Track
{
    public (double X, double Y)[] Waypoints { get; }
    public double TrackWidth { get; } = 80;
    public int TotalLaps { get; } = 3;

    public Track()
    {
        // 타원형 서킷 (중심 400,300)
        var pts = new List<(double, double)>();
        int segments = 40;
        for (int i = 0; i < segments; i++)
        {
            double angle = 2 * Math.PI * i / segments;
            double rx = 280 + 30 * Math.Sin(angle * 3); // 약간 불규칙
            double ry = 200 + 20 * Math.Cos(angle * 2);
            double x = 400 + rx * Math.Cos(angle);
            double y = 300 + ry * Math.Sin(angle);
            pts.Add((x, y));
        }
        Waypoints = [.. pts];
    }

    public int NextWaypoint(int current) => (current + 1) % Waypoints.Length;

    public double DistanceToWaypoint(double x, double y, int wpIndex)
    {
        var (wx, wy) = Waypoints[wpIndex];
        return Math.Sqrt((x - wx) * (x - wx) + (y - wy) * (y - wy));
    }

    public (double Angle, double Dist) DirectionTo(double x, double y, int wpIndex)
    {
        var (wx, wy) = Waypoints[wpIndex];
        double dx = wx - x, dy = wy - y;
        return (Math.Atan2(dy, dx), Math.Sqrt(dx * dx + dy * dy));
    }

    public double TotalLength()
    {
        double total = 0;
        for (int i = 0; i < Waypoints.Length; i++)
        {
            var (x1, y1) = Waypoints[i];
            var (x2, y2) = Waypoints[NextWaypoint(i)];
            total += Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        }
        return total;
    }
}
