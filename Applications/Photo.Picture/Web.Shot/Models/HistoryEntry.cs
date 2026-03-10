namespace WebShot.Models;

public class HistoryEntry
{
    public string   Url           { get; set; } = string.Empty;
    public string   FilePath      { get; set; } = string.Empty;
    public string   FileType      { get; set; } = "png";   // "png" | "pdf"
    public DateTime CapturedAt    { get; set; } = DateTime.Now;
    public int      ViewportWidth { get; set; } = 1280;

    public string DisplayTime => CapturedAt.ToString("MM-dd HH:mm:ss");
    public string ShortUrl    => Url.Length > 60 ? Url[..60] + "…" : Url;
}
