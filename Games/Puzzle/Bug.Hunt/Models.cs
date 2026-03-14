using System.Windows.Media;

namespace BugHunt;

public class CodeLine
{
    public int LineNumber { get; set; }
    public string Code { get; set; } = "";
    public Brush ForeColor { get; set; } = Brushes.LightGray;
    public Brush BackColor { get; set; } = Brushes.Transparent;
    public bool IsBug { get; set; }
}

public record Puzzle(
    string Language,
    string Difficulty,      // "junior" | "senior"
    string Description,     // 문제 설명
    string[] Lines,         // 코드 줄
    int[] BugLines,         // 1-indexed 버그 줄 번호
    string Explanation      // 버그 설명
);
