using SkiaSharp;
using ZXing.QrCode.Internal;

namespace QrForge.Models;

public enum MarkerStyle { Square, Round, Dot }

public class QrStyle
{
    public SKColor ForeColor  { get; set; } = SKColors.Black;
    public SKColor BackColor  { get; set; } = SKColors.White;
    public MarkerStyle Marker { get; set; } = MarkerStyle.Square;
    public string LogoPath    { get; set; } = string.Empty;
    public ErrorCorrectionLevel EcLevel { get; set; } = ErrorCorrectionLevel.H;
}
