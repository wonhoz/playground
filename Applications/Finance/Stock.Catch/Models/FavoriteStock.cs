namespace Stock.Catch.Models;

/// <summary>즐겨찾기 종목(코드 + 이름). 콤보 표시는 ToString을 사용한다.</summary>
public sealed class FavoriteStock
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrEmpty(Name) ? Code : $"{Code}  {Name}";
}
