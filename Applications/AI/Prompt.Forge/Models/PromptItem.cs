namespace Prompt.Forge.Models;

public sealed class PromptItem
{
    public int     Id          { get; set; }
    public string  Title       { get; set; } = "";
    public string  Content     { get; set; } = "";
    public string  Tags        { get; set; } = "";
    public string  Service     { get; set; } = "";  // GPT-4, Claude, Gemini 등
    public bool    IsFavorite  { get; set; }
    public int     Version     { get; set; } = 1;
    public int     UseCount    { get; set; }
    public string  Notes       { get; set; } = "";
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    // 버전 히스토리에서 부모 ID
    public int?    ParentId    { get; set; }

    public int     SortOrder   { get; set; }

    /// {{변수명}} 목록 추출
    public List<string> ExtractVariables()
    {
        var matches = Regex.Matches(Content, @"\{\{(\w+)\}\}");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }
}
