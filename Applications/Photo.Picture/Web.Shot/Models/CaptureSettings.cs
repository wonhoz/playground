namespace WebShot.Models;

public class CaptureSettings
{
    public string Url          { get; set; } = string.Empty;
    public int    ViewportWidth { get; set; } = 1280;
    public int    DelayMs       { get; set; } = 1500;
    public bool   CapturePdf    { get; set; } = false;
    public string OutputFolder  { get; set; } = DefaultOutputFolder;

    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WebShot", "Captures");
}
