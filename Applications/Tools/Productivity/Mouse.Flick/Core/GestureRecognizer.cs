namespace MouseFlick.Core;

/// <summary>
/// Point[] → 방향 문자열 변환 (R/L/U/D 조합)
/// 화면 좌표계: R(→), D(↓), L(←), U(↑)
/// </summary>
internal static class GestureRecognizer
{
    public static string? Recognize(IReadOnlyList<Point> points, int minSegmentLength = 30)
    {
        if (points.Count < 2) return null;

        var  directions = new List<char>();
        char? currentDir = null;
        int  segLen      = 0;

        for (int i = 1; i < points.Count; i++)
        {
            double dx   = points[i].X - points[i - 1].X;
            double dy   = points[i].Y - points[i - 1].Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 2) continue;  // 극소 이동 무시

            char dir = SnapDirection(dx, dy);
            segLen += (int)dist;

            if (dir == currentDir) continue;

            // 방향 변경 시 이전 세그먼트 처리
            if (currentDir.HasValue && segLen >= minSegmentLength)
            {
                if (directions.Count == 0 || directions[^1] != currentDir.Value)
                    directions.Add(currentDir.Value);
            }

            currentDir = dir;
            segLen     = (int)dist;
        }

        // 마지막 세그먼트
        if (currentDir.HasValue && segLen >= minSegmentLength)
        {
            if (directions.Count == 0 || directions[^1] != currentDir.Value)
                directions.Add(currentDir.Value);
        }

        if (directions.Count == 0) return null;
        if (directions.Count > 4)  directions = directions.Take(4).ToList();

        return new string(directions.ToArray());
    }

    // 화면 좌표: Y 아래 방향이 양수
    private static char SnapDirection(double dx, double dy)
    {
        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        // Atan2: 우(0°), 하(90°), 좌(±180°), 상(-90°)
        return Math.Abs(angle) <= 45         ? 'R'
             : angle is > 45 and <= 135      ? 'D'
             : angle > 135 || angle <= -135  ? 'L'
             : /* -135 < angle < -45 */        'U';
    }
}
