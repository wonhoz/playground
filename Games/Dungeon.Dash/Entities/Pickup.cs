using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DungeonDash.Entities;

public enum PickupKind { Health, AtkBoost, SpeedBoost }

public sealed class Pickup
{
    public double X { get; }
    public double Y { get; }
    public const double Size = 12;
    public PickupKind Kind { get; }
    public bool Collected { get; set; }
    public Canvas Visual { get; }

    public Pickup(PickupKind kind, double x, double y)
    {
        Kind = kind;
        X = x; Y = y;

        Visual = new Canvas { Width = Size, Height = Size };

        var (color, text) = kind switch
        {
            PickupKind.Health => (Color.FromRgb(0x2E, 0xCC, 0x71), "♥"),
            PickupKind.AtkBoost => (Color.FromRgb(0xE7, 0x4C, 0x3C), "⚔"),
            PickupKind.SpeedBoost => (Color.FromRgb(0x00, 0xBB, 0xFF), "★"),
            _ => (Colors.White, "?")
        };

        var bg = new Ellipse
        {
            Width = Size, Height = Size,
            Fill = new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1
        };

        var label = new TextBlock
        {
            Text = text,
            FontSize = 8,
            Foreground = new SolidColorBrush(Colors.White)
        };
        Canvas.SetLeft(label, 1);
        Canvas.SetTop(label, -1);

        Visual.Children.Add(bg);
        Visual.Children.Add(label);
    }

    public Rect Bounds => new(X, Y, Size, Size);

    public void Apply(Player player)
    {
        switch (Kind)
        {
            case PickupKind.Health:
                player.Heal(30);
                break;
            case PickupKind.AtkBoost:
                player.Atk += 5;
                break;
            case PickupKind.SpeedBoost:
                player.DashSpeed += 50;
                break;
        }
        Collected = true;
    }

    public void SyncPosition(double camX, double camY)
    {
        Canvas.SetLeft(Visual, X - camX);
        Canvas.SetTop(Visual, Y - camY);
    }
}
