namespace ClipboardStacker.Models;

public class ClipEntry
{
    public Guid     Id        { get; init; } = Guid.NewGuid();
    public string   Text      { get; init; } = "";
    public DateTime CopiedAt  { get; init; } = DateTime.Now;
    public bool     IsPinned  { get; set; }

    /// <summary>UI 표시용 미리보기 (최대 60자)</summary>
    public string Preview => Text.Length > 60
        ? Text[..57].Replace('\n', ' ').Replace('\r', ' ') + "..."
        : Text.Replace('\n', ' ').Replace('\r', ' ');

    public ClipEntry(string text) => Text = text;
}
