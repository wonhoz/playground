using System.Windows.Controls;
using System.Windows.Media;

namespace DungeonDash.Entities;

public sealed class DamageText
{
    public double X { get; }
    public double Y { get; set; }
    public double Life { get; private set; }
    public bool IsAlive => Life > 0;
    public TextBlock Visual { get; }

    private readonly double _maxLife = 0.6;

    public DamageText(double x, double y, int damage, bool isPlayerDamage)
    {
        X = x; Y = y;
        Life = _maxLife;

        Visual = new TextBlock
        {
            Text = damage.ToString(),
            FontSize = isPlayerDamage ? 14 : 12,
            FontWeight = System.Windows.FontWeights.Bold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(isPlayerDamage
                ? Color.FromRgb(0xE7, 0x4C, 0x3C)
                : Color.FromRgb(0xFF, 0xD7, 0x00))
        };
    }

    public void Update(double dt)
    {
        Life -= dt;
        Y -= 40 * dt; // 위로 떠오름
        Visual.Opacity = Math.Max(0, Life / _maxLife);
    }

    public void SyncPosition(double camX, double camY)
    {
        Canvas.SetLeft(Visual, X - camX);
        Canvas.SetTop(Visual, Y - camY);
    }
}
