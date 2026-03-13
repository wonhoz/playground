namespace ComicCast.Models;

/// <summary>만화책 아카이브 형식</summary>
public enum ArchiveType { Folder, Cbz, Cbr, Cb7, Cbt }

/// <summary>보기 모드</summary>
public enum ViewMode { Single, DoubleManga, DoubleWebtoon }

/// <summary>만화책 메타데이터 (ComicInfo.xml 파싱 + DB 저장)</summary>
public class ComicBook
{
    public int     Id          { get; set; }
    public string  FilePath    { get; set; } = "";
    public string  Title       { get; set; } = "";
    public string  Series      { get; set; } = "";
    public string  Writer      { get; set; } = "";
    public string  Summary     { get; set; } = "";
    public int     Volume      { get; set; }
    public int     Number      { get; set; }   // 챕터 번호
    public int     PageCount   { get; set; }
    public int     LastPage    { get; set; }   // 마지막 읽은 페이지
    public DateTime AddedAt    { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; set; }
    public ArchiveType ArchiveType { get; set; }
    public string? CoverPath   { get; set; }   // 썸네일 캐시 경로

    [JsonIgnore]
    public bool IsRead => PageCount > 0 && LastPage >= PageCount - 1;

    [JsonIgnore]
    public double ReadPercent => PageCount > 0 ? (double)LastPage / PageCount * 100 : 0;
}

/// <summary>라이브러리 폴더 등록</summary>
public class LibraryFolder
{
    public int    Id       { get; set; }
    public string Path     { get; set; } = "";
    public string Label    { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
