namespace DodgeCraft.Entities;

/// <summary>탄환 (적 발사체)</summary>
public class Bullet
{
    public double X  { get; set; }
    public double Y  { get; set; }
    public double VX { get; set; }
    public double VY { get; set; }
    public double Radius     { get; set; } = 5;
    public Color  Color      { get; set; } = Colors.OrangeRed;
    public bool   IsAlive    { get; set; } = true;
    public bool   IsTracking { get; set; }
    public double TrackStrength { get; set; }  // 추적 가속도 (px/s²)
    public double AngularVel { get; set; }     // 회전 탄 (rad/s)
    public bool   Penetrating { get; set; }   // 구조물 관통

    public void Update(double dt, double playerX, double playerY)
    {
        // 추적 탄: 플레이어 방향으로 서서히 꺾임
        if (IsTracking && TrackStrength > 0)
        {
            double dx = playerX - X, dy = playerY - Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 1)
            {
                VX += dx / len * TrackStrength * dt;
                VY += dy / len * TrackStrength * dt;
                // 속도 크기 유지
                double spd = Math.Sqrt(VX * VX + VY * VY);
                double origSpd = 150;
                if (spd > origSpd * 1.5) { VX = VX / spd * origSpd; VY = VY / spd * origSpd; }
            }
        }

        // 회전 탄
        if (AngularVel != 0)
        {
            double speed  = Math.Sqrt(VX * VX + VY * VY);
            double angle  = Math.Atan2(VY, VX) + AngularVel * dt;
            VX = Math.Cos(angle) * speed;
            VY = Math.Sin(angle) * speed;
        }

        X += VX * dt;
        Y += VY * dt;
    }

    public bool HitTest(double px, double py, double pr) =>
        (X - px) * (X - px) + (Y - py) * (Y - py) < (Radius + pr) * (Radius + pr);
}
