namespace WebShot.Models;

public class AppSettings
{
    public int    ViewportWidth { get; set; } = 1280;
    public int    DelayMs       { get; set; } = 1000;
    public bool   CapturePdf    { get; set; } = false;
    public bool   HidePreview   { get; set; } = false;
    public string OutputFolder  { get; set; } = CaptureSettings.DefaultOutputFolder;
}
