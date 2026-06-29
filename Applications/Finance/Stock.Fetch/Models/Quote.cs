namespace Stock.Fetch.Models;

/// <summary>실시간 현재가 스냅샷(모니터링용).</summary>
public sealed record Quote(
    string Code,
    decimal Price,        // 현재가
    decimal ChangeRate,   // 전일 대비 등락률(%)
    DateTime Time);
