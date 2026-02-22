namespace GravityFlip.Entities;

public enum PlatformType { Normal, Moving, Crumbling, Bouncy }

public sealed class Platform
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public PlatformType Type { get; }

    // Moving platform
    public double MoveStartY { get; set; }
    public double MoveEndY { get; set; }
    public double MoveSpeed { get; set; }
    private int _moveDir = 1;

    // Crumbling platform
    public bool IsCrumbling { get; private set; }
    public double CrumbleTimer { get; private set; }
    public bool IsDestroyed { get; private set; }

    public Platform(double x, double y, double width, double height, PlatformType type)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Type = type;
    }

    public void Update(double dt)
    {
        if (Type == PlatformType.Moving)
        {
            Y += MoveSpeed * _moveDir * dt;
            if (Y <= MoveStartY) { Y = MoveStartY; _moveDir = 1; }
            else if (Y >= MoveEndY) { Y = MoveEndY; _moveDir = -1; }
        }

        if (Type == PlatformType.Crumbling && IsCrumbling)
        {
            CrumbleTimer -= dt;
            if (CrumbleTimer <= 0) IsDestroyed = true;
        }
    }

    public void StartCrumble()
    {
        if (Type != PlatformType.Crumbling || IsCrumbling) return;
        IsCrumbling = true;
        CrumbleTimer = 0.5;
    }

    public void Reset()
    {
        IsCrumbling = false;
        CrumbleTimer = 0;
        IsDestroyed = false;
    }
}
