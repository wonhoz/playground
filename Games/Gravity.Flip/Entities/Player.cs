namespace GravityFlip.Entities;

public sealed class Player
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocityY { get; set; }
    public bool GravityDown { get; set; } = true;
    public bool IsGrounded { get; set; }
    public bool IsDead { get; set; }

    public const double Size = 20;
    public const double Gravity = 800;
    public const double MaxFallSpeed = 400;

    public void FlipGravity()
    {
        if (IsDead) return;
        GravityDown = !GravityDown;
        VelocityY = 0;
    }

    public void Update(double dt)
    {
        if (IsDead) return;

        double gravDir = GravityDown ? 1.0 : -1.0;
        VelocityY += Gravity * gravDir * dt;

        if (VelocityY > MaxFallSpeed) VelocityY = MaxFallSpeed;
        if (VelocityY < -MaxFallSpeed) VelocityY = -MaxFallSpeed;

        Y += VelocityY * dt;
    }

    public void Reset(double x, double y)
    {
        X = x;
        Y = y;
        VelocityY = 0;
        GravityDown = true;
        IsGrounded = false;
        IsDead = false;
    }
}
