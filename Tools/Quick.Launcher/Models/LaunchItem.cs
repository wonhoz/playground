namespace QuickLauncher.Models;

public enum LaunchItemType { App, Url, Snippet }

public class LaunchItem
{
    public string Name    { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Icon    { get; set; } = "";
    public LaunchItemType Type   { get; set; }
    public string Target  { get; set; } = "";
    public int    Score   { get; set; }
}
