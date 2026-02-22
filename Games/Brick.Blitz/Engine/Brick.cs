using System.Windows.Media;

namespace BrickBlitz.Engine;

public enum BrickType
{
    Normal,
    Hard,
    Unbreakable
}

public sealed class Brick
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 50;
    public double Height { get; set; } = 18;
    public BrickType Type { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public Color Color { get; set; }
    public int Points { get; set; }
    public bool Alive { get; set; } = true;

    public Brick(double x, double y, BrickType type, Color color, int points)
    {
        X = x;
        Y = y;
        Type = type;
        Color = color;
        Points = points;

        switch (type)
        {
            case BrickType.Normal:
                Hp = 1;
                MaxHp = 1;
                break;
            case BrickType.Hard:
                Hp = 2;
                MaxHp = 2;
                break;
            case BrickType.Unbreakable:
                Hp = 999;
                MaxHp = 999;
                break;
        }
    }

    public bool Hit()
    {
        if (Type == BrickType.Unbreakable) return false;
        Hp--;
        if (Hp <= 0)
        {
            Alive = false;
            return true;
        }
        return false;
    }
}
