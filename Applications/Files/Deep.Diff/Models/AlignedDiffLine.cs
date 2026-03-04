namespace DeepDiff.Models;

public record TextSegment(string Text, bool IsHighlighted);

public class AlignedDiffLine
{
    public int? LeftLineNum  { get; set; }
    public int? RightLineNum { get; set; }
    public LineStatus Status { get; set; }
    public List<TextSegment> LeftSegments  { get; set; } = [];
    public List<TextSegment> RightSegments { get; set; } = [];

    public string LeftText  => string.Concat(LeftSegments.Select(s  => s.Text));
    public string RightText => string.Concat(RightSegments.Select(s => s.Text));

    public string LeftLineNumText  => LeftLineNum.HasValue  ? LeftLineNum.Value.ToString()  : "";
    public string RightLineNumText => RightLineNum.HasValue ? RightLineNum.Value.ToString() : "";

    public string BackgroundKey => Status switch
    {
        LineStatus.Changed   => "DiffChg",
        LineStatus.LeftOnly  => "DiffDel",
        LineStatus.RightOnly => "DiffAdd",
        _                    => "DiffSame"
    };
}

public record HexDiffRow(
    long Address,
    byte[] LeftBytes,
    byte[] RightBytes,
    bool[] BytesDiffer,
    string LeftDisplay,
    string RightDisplay,
    bool HasDiff);
