namespace CodeSnap.Models;

public class SnapConfig
{
    public string Language       { get; set; } = "Text";
    public CodeTheme Theme       { get; set; } = CodeTheme.Dracula;
    public string FontFamily     { get; set; } = "Cascadia Code";
    public int FontSize          { get; set; } = 14;
    public BackgroundType BgType { get; set; } = BackgroundType.Gradient;
    public int GradientIndex     { get; set; } = 0;
    public string SolidColor     { get; set; } = "#1E1B4B";
    public int Padding           { get; set; } = 40;
    public bool RoundCorners     { get; set; } = true;
    public bool ShowShadow       { get; set; } = true;
    public bool ShowWindowDeco   { get; set; } = true;
    public bool ShowLineNumbers  { get; set; } = false;
    public string FileName       { get; set; } = "";
}
