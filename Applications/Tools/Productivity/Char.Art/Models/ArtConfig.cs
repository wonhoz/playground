namespace CharArt.Models;

public class ArtConfig
{
    public string CharSetName { get; set; } = "ASCII 기본";
    public string CustomChars { get; set; } = "";
    public string FontFamily  { get; set; } = "Consolas";
    public double FontSize    { get; set; } = 8.0;
    public int    Columns     { get; set; } = 80;
    public bool   Invert      { get; set; } = false;
}
