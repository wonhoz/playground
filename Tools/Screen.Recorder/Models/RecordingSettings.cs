namespace ScreenRecorder.Models;

public class RecordingSettings
{
    public int FrameRate { get; set; } = 15;
    public string OutputFormat { get; set; } = "mp4";
    public string OutputFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    public bool ShowCursor { get; set; } = true;

    public static RecordingSettings CreateDefault() => new();
}
