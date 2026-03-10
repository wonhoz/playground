namespace WordCloud.Models;

public class CloudConfig
{
    public int MaxWords { get; set; } = 100;
    public int MinFreq { get; set; } = 2;
    public CloudShape Shape { get; set; } = CloudShape.Circle;
    public TextOrientation Orientation { get; set; } = TextOrientation.Mixed;
    public string FontName { get; set; } = "맑은 고딕";
    public int ThemeIndex { get; set; } = 0;
    public SKColor BgColor { get; set; } = new SKColor(0x0D, 0x0D, 0x16);
}
