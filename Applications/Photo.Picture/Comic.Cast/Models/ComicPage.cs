namespace ComicCast.Models;

/// <summary>아카이브에서 로드된 단일 페이지 정보</summary>
public class ComicPage
{
    public int    Index    { get; init; }
    public string Name     { get; init; } = "";
    /// <summary>아카이브 내부 경로 또는 실제 파일 경로</summary>
    public string EntryKey { get; init; } = "";
    public long   Size     { get; init; }
}

/// <summary>읽기 세션 상태</summary>
public class ReadingSession
{
    public ComicBook      Book     { get; set; } = null!;
    public List<ComicPage> Pages   { get; set; } = [];
    public int            Current  { get; set; }
    public ViewMode       ViewMode { get; set; } = ViewMode.Single;
    public double         Zoom     { get; set; } = 1.0;

    public bool HasPrev => Current > 0;
    public bool HasNext => Current < Pages.Count - 1;
}
