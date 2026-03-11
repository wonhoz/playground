namespace DodgeCraft.Engine;

/// <summary>적 탄막 패턴 생성기</summary>
public static class BulletPatterns
{
    private static readonly Random _rng = new();

    public enum PatternType { Radial, Aimed, Wave, Spiral }

    /// <summary>지정 패턴으로 탄환 목록 생성</summary>
    public static List<Bullet> Spawn(
        PatternType pattern,
        double originX, double originY,
        double targetX, double targetY,
        int wave)
    {
        double speed = 120 + wave * 20;
        speed = Math.Min(speed, 320);

        return pattern switch
        {
            PatternType.Radial  => SpawnRadial(originX, originY, 8 + wave, speed),
            PatternType.Aimed   => SpawnAimed(originX, originY, targetX, targetY, speed, wave),
            PatternType.Wave    => SpawnWave(originX, originY, targetX, targetY, speed, wave),
            PatternType.Spiral  => SpawnSpiral(originX, originY, speed, wave),
            _                   => [],
        };
    }

    private static List<Bullet> SpawnRadial(double ox, double oy, int count, double speed)
    {
        var list = new List<Bullet>();
        for (int i = 0; i < count; i++)
        {
            double angle = 2 * Math.PI * i / count;
            list.Add(new Bullet
            {
                X  = ox, Y = oy,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed,
                Radius = 5, Color = Colors.OrangeRed,
            });
        }
        return list;
    }

    private static List<Bullet> SpawnAimed(double ox, double oy, double tx, double ty, double speed, int wave)
    {
        var list = new List<Bullet>();
        int count = 1 + wave / 3;
        double spread = 0.15;

        double dx = tx - ox, dy = ty - oy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { dx = 0; dy = 1; } else { dx /= len; dy /= len; }

        for (int i = 0; i < count; i++)
        {
            double offset = (i - (count - 1) / 2.0) * spread;
            double cos = Math.Cos(offset), sin = Math.Sin(offset);
            double vx = dx * cos - dy * sin;
            double vy = dx * sin + dy * cos;
            list.Add(new Bullet
            {
                X = ox, Y = oy,
                VX = vx * speed, VY = vy * speed,
                Radius = 5, Color = Colors.Yellow,
                IsTracking = wave >= 4, TrackStrength = 40,
            });
        }
        return list;
    }

    private static List<Bullet> SpawnWave(double ox, double oy, double tx, double ty, double speed, int wave)
    {
        var list = new List<Bullet>();
        int rows = 2 + wave / 4;
        double gap = 26;

        double dx = tx - ox, dy = ty - oy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { dx = 0; dy = 1; } else { dx /= len; dy /= len; }
        double px = -dy, py = dx;

        for (int i = 0; i < rows; i++)
        {
            double offset = (i - (rows - 1) / 2.0) * gap;
            list.Add(new Bullet
            {
                X = ox + px * offset, Y = oy + py * offset,
                VX = dx * speed, VY = dy * speed,
                Radius = 5, Color = Colors.Cyan,
            });
        }
        return list;
    }

    private static List<Bullet> SpawnSpiral(double ox, double oy, double speed, int wave)
    {
        var list = new List<Bullet>();
        int arms = 3 + wave / 5;
        double baseAngle = _rng.NextDouble() * Math.PI * 2;
        for (int i = 0; i < arms; i++)
        {
            double angle = baseAngle + 2 * Math.PI * i / arms;
            list.Add(new Bullet
            {
                X = ox, Y = oy,
                VX = Math.Cos(angle) * speed,
                VY = Math.Sin(angle) * speed,
                Radius = 6, Color = Color.FromRgb(0xFF, 0x80, 0xFF),
                AngularVel = 1.2,
            });
        }
        return list;
    }
}
