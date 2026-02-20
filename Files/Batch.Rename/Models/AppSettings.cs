namespace BatchRename.Models;

public class AppSettings
{
    /// <summary>최근 사용 패턴 (최대 10개)</summary>
    public List<string> RecentPatterns { get; set; } = [];

    /// <summary>최근 사용 정규식 Find</summary>
    public List<string> RecentRegexFind { get; set; } = [];

    /// <summary>마지막으로 사용한 모드 (Pattern / Regex)</summary>
    public string LastMode { get; set; } = "Pattern";
}
