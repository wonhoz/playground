using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>데이터 소스 호출 실패 시 throw. 사용자 표시용 한글 메시지를 담는다.</summary>
public sealed class PriceSourceException(string message) : Exception(message);

/// <summary>
/// 종목 일봉(저가·고가·종가 등 OHLCV) 시세를 가져오는 데이터 소스 공통 인터페이스.
/// 네이버·Yahoo·KRX·KIS 4종이 이 계약을 구현한다.
/// </summary>
public interface IPriceSource
{
    SourceKind Kind { get; }

    /// <summary>UI·로그에 표시할 소스 이름.</summary>
    string DisplayName { get; }

    /// <summary>API 키가 필요한 소스인지(KIS만 true). 키 미설정 시 UI에서 안내.</summary>
    bool RequiresApiKey { get; }

    /// <summary>
    /// 지정 종목코드(6자리 단축코드)의 [from, to] 기간 일봉을 과거→현재 순서로 반환.
    /// </summary>
    Task<StockSeries> FetchAsync(string code, DateTime from, DateTime to, CancellationToken ct = default);
}
