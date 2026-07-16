namespace Stock.Catch.Models;

/// <summary>전광판 순위 종류: 급등 / 급락 / 거래량 급증(KR=거래증가율 · US=최다 거래).</summary>
public enum MoverKind { Gainers, Losers, VolumeSurge }

/// <summary>
/// 급등락 전광판 순위 항목 1건(국내·미국 공용).
/// <paramref name="Extra"/>는 종류별 부가 지표의 표시 문자열(KR 거래량 급증=전일比 거래증가율, US 급등락=거래대금 등).
/// </summary>
public sealed record MoverRow(
    int Rank,
    string Symbol,
    string Name,
    decimal Price,
    double ChangeRate,
    long Volume,
    string Extra = "")
{
    public string Display => string.IsNullOrEmpty(Name) ? Symbol : Name;
}
