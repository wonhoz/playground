namespace Stock.Fetch.Services;

/// <summary>미국 증시 세션 구분.</summary>
public enum UsSession { Closed, Pre, Regular, After }

/// <summary>
/// 미국 동부시간(ET·DST 자동) 기준으로 현재 세션을 판별한다.
/// 프리마켓 04:00~09:30, 정규장 09:30~16:00, 애프터마켓 16:00~20:00(평일).
/// </summary>
public static class UsMarket
{
    private static readonly TimeZoneInfo Et = ResolveEt();

    public static UsSession CurrentSession(DateTime? utc = null)
    {
        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(utc ?? DateTime.UtcNow, Et);
        if (nowEt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return UsSession.Closed;
        var t = nowEt.TimeOfDay;
        if (t >= new TimeSpan(4, 0, 0) && t < new TimeSpan(9, 30, 0)) return UsSession.Pre;
        if (t >= new TimeSpan(9, 30, 0) && t < new TimeSpan(16, 0, 0)) return UsSession.Regular;
        if (t >= new TimeSpan(16, 0, 0) && t < new TimeSpan(20, 0, 0)) return UsSession.After;
        return UsSession.Closed;
    }

    /// <summary>프리마켓 또는 애프터마켓(정규장 외 거래 시간) 여부.</summary>
    public static bool IsExtended(DateTime? utc = null)
        => CurrentSession(utc) is UsSession.Pre or UsSession.After;

    private static TimeZoneInfo ResolveEt()
    {
        foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* 다음 후보 시도 */ }
        }
        return TimeZoneInfo.Utc;
    }
}
