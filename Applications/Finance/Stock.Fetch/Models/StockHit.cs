namespace Stock.Fetch.Models;

/// <summary>종목 검색 결과 한 건(코드·이름·시장).</summary>
public sealed record StockHit(string Code, string Name, string Market)
{
    public override string ToString() =>
        string.IsNullOrEmpty(Market) ? $"{Code}  {Name}" : $"{Code}  {Name}  · {Market}";
}
