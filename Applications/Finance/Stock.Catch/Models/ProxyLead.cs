namespace Stock.Catch.Models;

/// <summary>
/// 야간(대상 ETF 휴장 시간) 프록시 선행 알림 설정 1건.
/// 미국 ETF(예: SOXL·SOXS·KORU)는 ET 20:00~04:00(한국 낮)엔 거래·데이터가 없어 놓치기 쉽다.
/// 그 시간대에도 거래되는 <b>상관 자산(프록시)</b>의 움직임으로 다음 세션의 상방/하방을 미리 알린다.
/// 실측(일봉 3개월 수익률): SOXL/SOXS↔NQ선물 상관 0.90(베타 ≈±7), KORU↔KOSPI 상관 0.47(베타 ≈1.75).
/// </summary>
public sealed class ProxyLead
{
    /// <summary>대상 ETF 티커(미국 · 예: SOXL). 이 종목이 <b>휴장</b>일 때만 프록시로 선행 알림한다.</summary>
    public string EtfSymbol { get; set; } = string.Empty;
    /// <summary>대상 ETF 표시명(예: SOXL 반도체 3x 롱).</summary>
    public string EtfName { get; set; } = string.Empty;

    /// <summary>프록시 티커(대상 휴장 시간에도 거래 · 예: NQ=F 나스닥선물, ^KS11 KOSPI).</summary>
    public string ProxySymbol { get; set; } = string.Empty;
    /// <summary>프록시 시세 소스(NQ=F·^KS11=Yahoo, 국내주식=Kis).</summary>
    public WatchSource ProxySource { get; set; } = WatchSource.Yahoo;
    /// <summary>프록시 표시명(예: NQ선물, KOSPI).</summary>
    public string ProxyLabel { get; set; } = string.Empty;

    /// <summary>
    /// 베타(부호 포함): 프록시 1% 상승 시 ETF의 기대 변화%. 실측 회귀. 예: SOXL +6.9, SOXS −7.0, KORU +1.75.
    /// 부호가 방향을 결정한다(음수=역상관 = 프록시↑면 ETF↓).
    /// </summary>
    public double Beta { get; set; } = 1.0;

    /// <summary>알림 임계(기대 ETF 변화% 단위). 프록시로 환산한 기대 ETF 움직임이 이 값의 배수를 새로 넘을 때 알림. 기본 2%.</summary>
    public double StepPct { get; set; } = 2.0;

    /// <summary>이 알림 전용 Slack 채널(비우면 대상 ETF 관심종목 채널 또는 전역 기본).</summary>
    public string SlackChannel { get; set; } = string.Empty;

    /// <summary>기본 프록시 매핑(설정이 비어 있을 때 시드) — SOXL/SOXS↔NQ선물, KORU↔KOSPI.</summary>
    public static List<ProxyLead> Defaults() => new()
    {
        new() { EtfSymbol = "SOXL", EtfName = "SOXL 반도체 3x 롱", ProxySymbol = "NQ=F", ProxySource = WatchSource.Yahoo, ProxyLabel = "NQ선물", Beta = 6.9, StepPct = 2.0 },
        new() { EtfSymbol = "SOXS", EtfName = "SOXS 반도체 3x 숏", ProxySymbol = "NQ=F", ProxySource = WatchSource.Yahoo, ProxyLabel = "NQ선물", Beta = -7.0, StepPct = 2.0 },
        new() { EtfSymbol = "KORU", EtfName = "KORU 한국 3x 롱", ProxySymbol = "^KS11", ProxySource = WatchSource.Yahoo, ProxyLabel = "KOSPI", Beta = 1.75, StepPct = 1.5 },
    };
}
