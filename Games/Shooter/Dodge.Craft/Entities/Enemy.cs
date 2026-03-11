namespace DodgeCraft.Entities;

/// <summary>적 (탄막 발사원)</summary>
public class Enemy
{
    private static readonly Random _rng = new();

    public double X       { get; set; }
    public double Y       { get; set; }
    public double VX      { get; set; }
    public double VY      { get; set; }
    public int    Hp      { get; set; } = 3;
    public bool   IsAlive { get; set; } = true;
    public double FireCooldown { get; set; }
    public double FireInterval { get; set; } = 2.0;
    public BulletPatterns.PatternType Pattern { get; set; }

    public void Update(double dt, double canvasW, double canvasH)
    {
        X += VX * dt;
        Y += VY * dt;

        // 벽 바운스
        if (X < 20 || X > canvasW - 20) VX = -VX;
        if (Y < 20 || Y > canvasH / 2)  VY = -VY;

        X = Math.Clamp(X, 20, canvasW - 20);
        Y = Math.Clamp(Y, 20, canvasH / 2);

        FireCooldown -= dt;
    }

    public bool CanFire() => FireCooldown <= 0;
    public void ResetFire() => FireCooldown = FireInterval + _rng.NextDouble() * 0.5;

    public void Hit()
    {
        Hp--;
        if (Hp <= 0) IsAlive = false;
    }
}
