namespace Stock.Catch.Models;

/// <summary>래더 알림 종류.</summary>
public enum LadderAlertKind
{
    /// <summary>현재가가 매수 지정가(1~4호가)에 닿음.</summary>
    BuyTouch,
    /// <summary>현재가가 익절 목표가를 돌파(보유·상승세).</summary>
    SellBreak,
    /// <summary>시초가가 갭다운 취소선 이하.</summary>
    GapDown
}

/// <summary>
/// 매수/익절 래더·갭다운 알림 1건. Level은 매수/익절 호가 번호(1~4), 갭다운은 0.
/// Target은 해당 기준가(매수 지정가·익절가·갭다운 취소선), Price는 현재가(갭다운은 시초가).
/// </summary>
public sealed record LadderAlert(
    string Code,
    string Name,
    LadderAlertKind Kind,
    int Level,
    decimal Price,
    decimal Target,
    string Detail,
    DateTime Time)
{
    public string Display => string.IsNullOrEmpty(Name) ? Code : $"{Name} ({Code})";
}
