namespace LinkVault.Models;

public class Bookmark
{
    public long Id { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";          // 콤마 구분 태그
    public int Stars { get; set; } = 0;             // 0~5
    public bool IsRead { get; set; } = false;
    public string? SnapshotPath { get; set; }       // 로컬 HTML 경로
    public string? FaviconPath { get; set; }        // 로컬 파비콘 경로
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>태그 목록을 배열로 반환</summary>
    public string[] TagList =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>표시용 호스트명</summary>
    public string Host
    {
        get
        {
            try { return new Uri(Url).Host; }
            catch { return ""; }
        }
    }

    /// <summary>별점 문자열</summary>
    public string StarsDisplay => Stars > 0 ? new string('★', Stars) + new string('☆', 5 - Stars) : "☆☆☆☆☆";

    public string ReadStatusDisplay => IsRead ? "읽음" : "미읽음";
}
