namespace Prompt.Forge.Models;

public sealed class PromptItem : INotifyPropertyChanged
{
    public int     Id          { get; set; }
    public string  Title       { get; set; } = "";
    public string  Content     { get; set; } = "";
    public string  Tags        { get; set; } = "";
    public string  Service     { get; set; } = "";  // GPT-4, Claude, Gemini 등
    public bool    IsFavorite  { get; set; }
    public int     Version     { get; set; } = 1;

    int _useCount;
    public int UseCount
    {
        get => _useCount;
        set { _useCount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseCount))); }
    }

    public string  Notes       { get; set; } = "";
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    // 버전 히스토리에서 부모 ID
    public int?    ParentId    { get; set; }

    public int     SortOrder   { get; set; }
    public bool    IsPinned    { get; set; }

    public IEnumerable<string> TagList =>
        string.IsNullOrEmpty(Tags) ? [] :
        Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// {{변수명}} 목록 추출 (영문, 숫자, _, 한글 지원)
    public List<string> ExtractVariables()
    {
        var matches = Regex.Matches(Content, @"\{\{([\w\p{L}]+)\}\}");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }
}
