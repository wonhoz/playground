namespace WinEvent.Models;

public sealed class AlertRule
{
    public string Name { get; set; } = "";

    /// <summary>null = 모든 레벨 / 1=위험 / 2=오류 / 3=경고 / 4=정보</summary>
    public int? Level { get; set; }

    /// <summary>null = 모든 EventID</summary>
    public long? EventId { get; set; }

    /// <summary>null = 모든 소스. 정규식 패턴 허용.</summary>
    public string? SourcePattern { get; set; }

    /// <summary>null = 메시지 미검사. 정규식 패턴 허용.</summary>
    public string? MessagePattern { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool Matches(EventItem item)
    {
        if (!IsEnabled) return false;
        if (Level.HasValue && item.Level != Level.Value) return false;
        if (EventId.HasValue && item.EventId != EventId.Value) return false;
        if (!string.IsNullOrEmpty(SourcePattern) &&
            !Regex.IsMatch(item.ProviderName, SourcePattern, RegexOptions.IgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(MessagePattern) &&
            !Regex.IsMatch(item.MessageFull, MessagePattern, RegexOptions.IgnoreCase))
            return false;
        return true;
    }
}
