using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FistFury.Entities;

/// <summary>
/// 타격 이펙트 (별/원 파티클).
/// </summary>
public sealed class HitEffect
{
    public double X { get; }
    public double Y { get; }
    public double Life { get; private set; }
    public bool IsAlive => Life > 0;
    public Canvas Visual { get; }

    private readonly double _maxLife;

    public HitEffect(double x, double y, Color color, bool isBig = false)
    {
        X = x; Y = y;
        _maxLife = isBig ? 0.5 : 0.3;
        Life = _maxLife;

        double size = isBig ? 40 : 24;
        Visual = new Canvas { Width = size, Height = size };

        // 스타버스트 이펙트
        var star = new Ellipse
        {
            Width = size, Height = size,
            Fill = new RadialGradientBrush(
                Color.FromArgb(200, color.R, color.G, color.B),
                Colors.Transparent)
        };
        Visual.Children.Add(star);

        // "POW" 텍스트 (큰 이펙트만)
        if (isBig)
        {
            var text = new TextBlock
            {
                Text = "POW!",
                FontSize = 12,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Consolas")
            };
            Canvas.SetLeft(text, 4);
            Canvas.SetTop(text, size / 2 - 8);
            Visual.Children.Add(text);
        }

        Canvas.SetLeft(Visual, x - size / 2);
        Canvas.SetTop(Visual, y - size / 2);
    }

    public void Update(double dt)
    {
        Life -= dt;
        Visual.Opacity = Math.Max(0, Life / _maxLife);
        double scale = 1 + (1 - Life / _maxLife) * 0.5;
        Visual.RenderTransform = new ScaleTransform(scale, scale,
            Visual.Width / 2, Visual.Height / 2);
    }
}
