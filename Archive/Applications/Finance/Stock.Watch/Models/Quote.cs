namespace Stock.Watch.Models;

/// <summary>
/// 현재가 스냅샷. KIS inquire-price 응답을 단순화한 모델.
/// </summary>
public sealed record Quote(
    string Code,
    decimal Price,
    decimal Change,
    decimal ChangeRate,
    long Volume,
    DateTime AsOf);
