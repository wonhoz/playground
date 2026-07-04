namespace Stock.Catch.Models;

/// <summary>보유 종목 평단 대비 수익률이 임계값을 넘었을 때 발생하는 알림.</summary>
public sealed record PortfolioAlert(
    string Code,
    string Name,
    decimal Price,        // 현재가
    decimal AvgPrice,     // 평단
    int Quantity,         // 보유 수량
    double ReturnPct,     // 평단 대비 수익률(%)
    double Threshold,     // 도달한 임계값(% · 부호 = 방향)
    DateTime Time)
{
    public bool IsUp => Threshold >= 0;
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";
    /// <summary>평가손익(원).</summary>
    public decimal EvalPL => (Price - AvgPrice) * Quantity;
}
