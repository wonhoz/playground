namespace DodgeCraft.Entities;

/// <summary>플레이어 배치 방어 구조물</summary>
public class Structure
{
    public enum StructureType { Wall, Mirror, Fan, Bomb }
    public StructureType Type    { get; set; }
    public double        X       { get; set; }
    public double        Y       { get; set; }
    public double        Angle   { get; set; }  // 배치 각도 (Mirror/Fan용)
    public int           Hp      { get; set; }  // 내구도 (Wall: 3, Mirror: 1, Fan: 2, Bomb: 1)
    public double        Lifetime { get; set; } = 30.0;  // 남은 수명 (초)
    public bool          IsAlive  { get; set; } = true;

    public double Width  => Type == StructureType.Fan ? 50 : 28;
    public double Height => Type == StructureType.Wall ? 60 : 28;

    // 탄환과 상호작용 반환값
    public enum InteractionResult { None, Blocked, Reflected, Deflected, Absorbed }

    public InteractionResult Interact(Bullet bullet)
    {
        if (!IsAlive || bullet.Penetrating) return InteractionResult.None;

        // 히트 체크 (AABB 간략화)
        double hw = Width / 2 + bullet.Radius;
        double hh = Height / 2 + bullet.Radius;
        if (Math.Abs(bullet.X - X) > hw || Math.Abs(bullet.Y - Y) > hh)
            return InteractionResult.None;

        return Type switch
        {
            StructureType.Wall   => HandleWall(bullet),
            StructureType.Mirror => HandleMirror(bullet),
            StructureType.Fan    => HandleFan(bullet),
            StructureType.Bomb   => HandleBomb(bullet),
            _                    => InteractionResult.None,
        };
    }

    private InteractionResult HandleWall(Bullet bullet)
    {
        bullet.IsAlive = false;
        Hp--;
        if (Hp <= 0) IsAlive = false;
        return InteractionResult.Blocked;
    }

    private InteractionResult HandleMirror(Bullet bullet)
    {
        // 거울: 입사각에서 90° 반사 (법선 = Angle 방향)
        double nx = Math.Cos(Angle), ny = Math.Sin(Angle);
        double dot = bullet.VX * nx + bullet.VY * ny;
        bullet.VX -= 2 * dot * nx;
        bullet.VY -= 2 * dot * ny;
        // 거울은 1회 사용 후 소멸
        IsAlive = false;
        return InteractionResult.Reflected;
    }

    private InteractionResult HandleFan(Bullet bullet)
    {
        // 팬: 탄 경로를 Angle 방향으로 45° 구부림
        double speed = Math.Sqrt(bullet.VX * bullet.VX + bullet.VY * bullet.VY);
        double curAngle = Math.Atan2(bullet.VY, bullet.VX);
        double targetAngle = curAngle + Angle;
        double newAngle = curAngle + (targetAngle - curAngle) * 0.6;
        bullet.VX = Math.Cos(newAngle) * speed;
        bullet.VY = Math.Sin(newAngle) * speed;
        Hp--;
        if (Hp <= 0) IsAlive = false;
        return InteractionResult.Deflected;
    }

    private InteractionResult HandleBomb(Bullet bullet)
    {
        bullet.IsAlive = false;
        IsAlive = false;
        return InteractionResult.Absorbed;
    }

    public void Update(double dt)
    {
        if (!IsAlive) return;
        Lifetime -= dt;
        if (Lifetime <= 0) IsAlive = false;
    }

    public static Structure Create(StructureType type, double x, double y, double angle = 0)
    {
        int hp = type switch
        {
            StructureType.Wall   => 5,
            StructureType.Mirror => 1,
            StructureType.Fan    => 3,
            StructureType.Bomb   => 1,
            _                    => 1,
        };
        return new Structure { Type = type, X = x, Y = y, Angle = angle, Hp = hp };
    }
}
