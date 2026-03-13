namespace CrosswordCast.Models;

public record WordEntry(string Word, string HintKo);

public class PlacedWord
{
    public string Word   { get; init; } = "";
    public string HintKo { get; init; } = "";
    public int    Row    { get; init; }
    public int    Col    { get; init; }
    public bool   Across { get; init; }
    public int    Number { get; set; }

    public string ClueText => $"{Number}. {HintKo}  ({Word.Length}자)";

    public bool Contains(int r, int c) => Across
        ? Row == r && Col <= c && c < Col + Word.Length
        : Col == c && Row <= r && r < Row + Word.Length;
}

public class Puzzle
{
    public const int N = 13;

    public List<PlacedWord> Words   { get; } = [];
    public char[,]          Grid    { get; } = new char[N, N]; // 정답 격자
    public int[,]           Numbers { get; } = new int[N, N];  // 셀 번호

    public IEnumerable<PlacedWord> AcrossWords => Words.Where(w => w.Across).OrderBy(w => w.Number);
    public IEnumerable<PlacedWord> DownWords   => Words.Where(w => !w.Across).OrderBy(w => w.Number);
}
