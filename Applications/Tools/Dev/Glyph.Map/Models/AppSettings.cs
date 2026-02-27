namespace GlyphMap.Models;

public sealed class AppSettings
{
    /// <summary>즐겨찾기 코드포인트 목록 (최대 10개, 무료)</summary>
    public List<int> Favorites { get; set; } = [];

    /// <summary>최근 사용 코드포인트 목록 (최대 30개)</summary>
    public List<int> Recent { get; set; } = [];

    /// <summary>마지막으로 선택한 블록 이름</summary>
    public string LastBlock { get; set; } = "";

    /// <summary>마지막 검색어</summary>
    public string LastSearch { get; set; } = "";

    public const int MaxFavoritesFree = 10;
    public const int MaxRecent = 30;

    public void AddRecent(int codePoint)
    {
        Recent.Remove(codePoint);
        Recent.Insert(0, codePoint);
        if (Recent.Count > MaxRecent) Recent.RemoveAt(Recent.Count - 1);
    }

    public bool ToggleFavorite(int codePoint)
    {
        if (Favorites.Contains(codePoint))
        {
            Favorites.Remove(codePoint);
            return false;
        }
        if (Favorites.Count < MaxFavoritesFree)
        {
            Favorites.Add(codePoint);
            return true;
        }
        return false; // 무료 한도 초과
    }

    public bool IsFavorite(int codePoint) => Favorites.Contains(codePoint);
}
