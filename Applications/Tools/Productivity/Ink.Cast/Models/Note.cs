namespace InkCast.Models;

/// <summary>노트 모델</summary>
public class Note
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<string> Tags { get; set; } = [];

    /// <summary>업데이트 시간 상대 표시 (예: "2시간 전")</summary>
    public string RelativeTime
    {
        get
        {
            var diff = DateTime.Now - UpdatedAt;
            return diff.TotalMinutes < 1   ? "방금 전"
                 : diff.TotalHours   < 1   ? $"{(int)diff.TotalMinutes}분 전"
                 : diff.TotalDays    < 1   ? $"{(int)diff.TotalHours}시간 전"
                 : diff.TotalDays    < 30  ? $"{(int)diff.TotalDays}일 전"
                 : UpdatedAt.ToString("yyyy.MM.dd");
        }
    }
}
