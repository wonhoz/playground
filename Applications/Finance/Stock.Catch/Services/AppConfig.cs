using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>
/// 앱 설정 영속화. 저장 위치는 리포 밖 %LocalAppData%\Playground\Stock.Catch\config.json
/// 이므로 KIS API 키가 소스 저장소에 절대 포함되지 않는다.
/// </summary>
public sealed class AppConfig
{
    // ── KIS Open API 자격(선택 — KIS 소스 사용 시에만 필요) ──
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>true=모의투자(:29443), false=실전(:9443). 시세 조회는 실전 권장.</summary>
    public bool UseMockServer { get; set; } = false;

    /// <summary>
    /// KIS 국내 현재가 조회 시장 구분(FID_COND_MRKT_DIV_CODE): "J"=KRX, "NX"=NXT(넥스트레이드),
    /// "UN"=통합(KRX+NXT). 통합/NXT면 장 마감 후 NXT 시간대(~20:00) 시세도 수신. 기본 통합.
    /// </summary>
    public string KisMarketDiv { get; set; } = "UN";

    // ── 해외 실시간 시세 키(관심 종목 미국 종목용 · 선택) ──
    /// <summary>Finnhub API Key(무료). 미국 종목 실시간 시세 소스.</summary>
    public string FinnhubApiKey { get; set; } = string.Empty;
    /// <summary>Alpaca API Key ID(무료). 미국 종목 실시간(IEX) 시세 소스.</summary>
    public string AlpacaApiKeyId { get; set; } = string.Empty;
    /// <summary>Alpaca API Secret Key.</summary>
    public string AlpacaApiSecret { get; set; } = string.Empty;

    [JsonIgnore] public bool HasFinnhubKey => !string.IsNullOrWhiteSpace(FinnhubApiKey);
    [JsonIgnore] public bool HasAlpacaKeys => !string.IsNullOrWhiteSpace(AlpacaApiKeyId) && !string.IsNullOrWhiteSpace(AlpacaApiSecret);

    // ── 즐겨찾기 종목 ──
    public List<FavoriteStock> Favorites { get; set; } = new();

    // ── 최근 사용 값(재실행 시 마지막 선택 복원) ──
    public SourceKind LastSource { get; set; } = SourceKind.Naver;
    public string LastCode { get; set; } = "005930";
    public string LastName { get; set; } = string.Empty;
    public string LastFrom { get; set; } = string.Empty;
    public string LastTo { get; set; } = string.Empty;
    public ExportFormat LastFormat { get; set; } = ExportFormat.Csv;
    /// <summary>마지막 선택 컬럼(빈 목록이면 전체).</summary>
    public List<CandleColumn> LastColumns { get; set; } = new();
    public bool LastIncludeHeader { get; set; } = true;
    public string LastExportDir { get; set; } = string.Empty;
    /// <summary>분봉 시그널 백테스트에서 마지막으로 CSV를 선택한 폴더.</summary>
    public string LastSignalDir { get; set; } = string.Empty;
    /// <summary>분봉 시그널 결과(_시그널.csv)를 마지막으로 저장한 폴더.</summary>
    public string LastSignalOutDir { get; set; } = string.Empty;

    // ── 차트 설정(재오픈 시 복원) ──
    public BarInterval ChartInterval { get; set; } = BarInterval.Day;
    public ChartSourceKind ChartSource { get; set; } = ChartSourceKind.Yahoo;
    public bool ChartBollinger { get; set; } = true;
    public bool ChartMa { get; set; } = true;
    public bool ChartRsi { get; set; } = true;
    public bool ChartVolume { get; set; } = true;
    public bool ChartAutoRefresh { get; set; } = false;
    public int ChartPeriodSec { get; set; } = 10;

    // ── 매수/익절 래더 설정(공격성·추세) ──
    /// <summary>매수 공격성 0(보수)~1(공격).</summary>
    public double LadderAggressiveness { get; set; } = 0.0;
    /// <summary>익절 강도 0(보수·도달↑)~1(공격·수익↑).</summary>
    public double LadderSellStrength { get; set; } = 0.0;
    /// <summary>추세 자동 반영(켜면 슬라이더 자동 산정).</summary>
    public bool LadderUseTrend { get; set; } = false;

    // ── 자산 포트폴리오 ──
    /// <summary>포트폴리오 JSON 저장 경로(비어 있으면 문서\StockCatch\portfolio.json). Dropbox 등 동기화 폴더 권장.</summary>
    public string PortfolioPath { get; set; } = string.Empty;
    /// <summary>내 자산 창에서 현재가 자동 갱신 사용.</summary>
    public bool PortfolioAutoRefresh { get; set; } = false;
    /// <summary>내 자산 현재가 자동 갱신 주기(초). 최소 10초.</summary>
    public int PortfolioRefreshSeconds { get; set; } = 60;
    /// <summary>매수/익절 래더·갭다운 알림을 켠 보유 종목 코드 목록(종목별 옵트인).</summary>
    public List<string> LadderHoldingCodes { get; set; } = new();

    // ── 관심 종목(워치리스트) ──
    public List<WatchItem> Watchlist { get; set; } = new();
    /// <summary>관심 종목 백그라운드 모니터링 활성화.</summary>
    public bool WatchEnabled { get; set; } = false;
    /// <summary>관심 종목 폴링 주기(초·임계값 체크). 최소 10초.</summary>
    public int WatchPollIntervalSeconds { get; set; } = 60;
    /// <summary>관심 종목 다이제스트 알림 주기(분). 0이면 비활성.</summary>
    public int WatchDigestIntervalMinutes { get; set; } = 0;
    /// <summary>프리마켓(08:00~08:50) 분위기 요약을 동시호가 직전(08:50) 1회 Slack 전송. 기본 켜짐.</summary>
    public bool WatchPreMarketSummary { get; set; } = true;
    /// <summary>애프터마켓(15:40~20:00) 분위기 요약을 장 종료(20:00) 후 1회 Slack 전송. 기본 켜짐.</summary>
    public bool WatchAfterMarketSummary { get; set; } = true;
    /// <summary>
    /// 관심 종목 전역 추세 조건 목록(여러 개 가능: 예 3분당 1%, 5분당 2%). 종목에 자체 조건이 없으면 이 목록을 사용.
    /// 기본 1개(3분당 2%).
    /// </summary>
    public List<TrendRule> WatchRules { get; set; } = new() { new TrendRule { WindowMinutes = 3, StepUp = 2, StepDown = 2 } };
    /// <summary>
    /// 미국 종목의 프리/애프터마켓 시간대에는 종목 소스와 무관하게 Yahoo에서 시세를 가져온다(무료 확장시간 실시간).
    /// 정규장에는 각 종목에 설정된 소스를 사용. 기본 켜짐.
    /// </summary>
    public bool WatchUseYahooExtended { get; set; } = true;

    // ── 보유 종목 모니터링 / Slack 알림 ──
    public string SlackWebhookUrl { get; set; } = string.Empty;
    /// <summary>전송 대상 기본 채널(예: "#stock"). 비우면 webhook 기본 채널. 관심 종목별 채널(WatchItem.SlackChannel)이 우선한다.</summary>
    public string SlackChannel { get; set; } = "#stock";
    /// <summary>트레이 상주 모니터링 활성화.</summary>
    public bool MonitorEnabled { get; set; } = false;
    /// <summary>폴링 주기(초). 최소 10초.</summary>
    public int MonitorIntervalSeconds { get; set; } = 60;
    /// <summary>장 시간(09:00~15:30 평일)에만 폴링.</summary>
    public bool MonitorMarketHoursOnly { get; set; } = true;
    /// <summary>평단 대비 수익률 알림 임계값(% · 상승/하락 양방향). 기본 ±2/5/7/10/12.</summary>
    public List<double> AlertThresholds { get; set; } = new() { 2, 5, 7, 10, 12 };
    /// <summary>시세 조회가 연속 N회 실패하면 알림(보유·관심 종목 공통). 0이면 끔. 기본 3회.</summary>
    public int FetchFailAlertThreshold { get; set; } = 3;

    /// <summary>한국·미국 장 세션(오픈·동시호가·마감 등) 5분 전 Slack·트레이 알림. 기본 켜짐.</summary>
    public bool MarketScheduleAlerts { get; set; } = true;

    /// <summary>추세 알림에 반등(반전) 확률 추정을 첨부(휴리스틱). 기본 켜짐.</summary>
    public bool WatchReversalEstimate { get; set; } = true;
    /// <summary>반등 점수 → 과거 적중률 보정 곡선(백테스트 학습 결과). 널이면 raw 점수 사용.</summary>
    public ReversalCalibration? ReversalCalibration { get; set; }

    // ── 바닥 반등 시그널(관심 종목 · 국내 1분봉 · KIS 분봉 필요) ──
    /// <summary>
    /// 셋업으로 인정할 RSI(14) 과매도 상한. 밴드 터치 구간 최저 RSI가 이 값 이하일 때만 시그널.
    /// 실측(0193T0 07.02~03 백테스트): 진짜 V바닥 저점 RSI 21~34 → 30이면 일부 누락, 35 권장.
    /// </summary>
    public double BottomRsiMax { get; set; } = 35;
    /// <summary>
    /// 거래량 급증 배수(1분봉 20봉 평균 대비). 터치 구간 최대 분봉 거래량 기준.
    /// 실측: 급락 시 20봉 평균 자체가 부풀어 진짜 V바닥도 1.6~2.1× → 2.0이면 절반 누락, 1.5 권장.
    /// </summary>
    public double BottomVolumeRatio { get; set; } = 1.5;
    /// <summary>볼린저 하단 터치를 찾는 최근 완성봉 수.</summary>
    public int BottomTouchLookback { get; set; } = 5;
    /// <summary>1차 시그널 후 MA5/MA20 골든크로스 확인(2차) 알림 사용.</summary>
    public bool BottomConfirmCross { get; set; } = true;
    /// <summary>
    /// 골든크로스(✅🔥) 후 이 봉 수(×tf)가 지나도 종가가 GC 가격 이상이면 "🚀 진입 적기" 확인 알림.
    /// 0=끔. 실측(14일 GC 114건): GC 즉시 진입 승률 57%·오탐 16% → +2봉 지속확인 82%·2%(🔥는 38%→13%).
    /// GC에 무작정 진입하는 오탐을 거르는 3차 확인. 기본 2.
    /// </summary>
    public int BottomHoldConfirmBars { get; set; } = 2;
    /// <summary>
    /// 1차 시그널 직후 첫 완성봉이 양봉이면 "반등 지속" 조기 확인 알림(골든크로스보다 1~5분 빠름).
    /// 실측: 진짜 반등 3/4 직후 양봉, 가짜 2/3 직후 음봉 — 강/약 구분 힌트.
    /// </summary>
    public bool BottomFollowCandle { get; set; } = true;
    /// <summary>골든크로스 확인 대기 시간(분).</summary>
    public int BottomConfirmWindowMinutes { get; set; } = 20;
    /// <summary>같은 종목 1차 시그널 재알림 쿨다운(분).</summary>
    public int BottomCooldownMinutes { get; set; } = 15;
    /// <summary>
    /// 골든크로스 모멘텀 임계(%): 1차 시그널 이후 이 비율 이상 올라 있어야 '반등 확인(강)'.
    /// 미달이면 '약한 확인'으로 구분 알림. 실측: 가짜 GC +0.51% vs 진짜 +0.90~6.73% → 기본 0.8.
    /// </summary>
    public double BottomGcMinRisePct { get; set; } = 0.8;
    /// <summary>
    /// '강력 확인(🔥)' 모멘텀 임계(%): 1차→GC 상승률이 이 이상이면 최상위 등급으로 표시.
    /// 실측: 건당 기대수익이 모멘텀 구간별 단조 증가(0.8~1.5% +0.11 → 2.5%+ +0.41%/건) → 기본 2.0.
    /// </summary>
    public double BottomGcStrongPct { get; set; } = 2.0;
    /// <summary>
    /// 일봉 추세 컨텍스트 표기: 완성 일봉 기반 래더 추세점수(−1~+1)와 갭을 반등·GC 알림에 표기.
    /// 강등이 아닌 정보 제공 — 전수 검증에서 최고 시그널(07-03 10:05 +6.73%)이 폭락 직후
    /// (추세 −1.00)에 나왔고, 추세 +1.00 통과 GC의 실전 수익 합계 ≈0이라 강등은 폐기. 기본 켜짐.
    /// </summary>
    public bool BottomTrendGate { get; set; } = true;
    /// <summary>
    /// 시그널 판정 타임프레임(분) 목록. 1=원본 1분봉, 그 외는 1분봉을 롤링(convolution) 집계한
    /// N분 봉으로 같은 로직을 독립 판정("멀리서 본" 거시 시그널). 쿨다운·확인 창은 tf에 비례.
    /// 기본 1·3·5·10·15분.
    /// </summary>
    public List<int> SignalTimeframes { get; set; } = new() { 1, 3, 5, 10, 15 };
    /// <summary>
    /// 밴드워킹 필터: 최근 10봉 중 하단 터치 봉이 이 수를 초과하면 지속 하락으로 보고 스킵.
    /// 실측: 깊은 급락 V바닥은 터치 5~7봉이라 4는 진짜 반등까지 차단 → 7 권장(8봉+만 스킵).
    /// </summary>
    public int BottomWalkMaxTouches { get; set; } = 7;
    /// <summary>트리거 봉 종가의 최소 %b(밴드 하단 0~상단 1). 이 이상 회복 마감해야 시그널(약반등 필터).</summary>
    public double BottomMinPercentB { get; set; } = 0.15;
    /// <summary>
    /// 거래량 급증 탐색 창(완성봉 수). 0=터치 구간만(구 방식), N=최근 N봉.
    /// 실측(0197X0 07-02 11:12): 투매 거래량 피크가 RSI 상승 전환보다 5~8분 일러 터치 구간(≤5봉)
    /// 밖으로 밀리는 케이스 발견. 14일×3종목 A/B 전수: 반등 승률 48→50% · 확인GC 55→56%(n 67→78) ·
    /// 직후양봉 39→44% · 07-03 레버리지 핵심 시그널 무변화 → 10 채택.
    /// </summary>
    public int BottomVolWindowBars { get; set; } = 10;
    /// <summary>
    /// 짧은 볼린저 병행 셋업 기간(0=끔). 기본 볼린저(20)가 개장 급락으로 과도하게 넓어지면 이후
    /// '두 번째 저점(이중 바닥)'이 20-하단에 안 닿아 누락되는데, 짧은 기간(10) 볼린저는 급락 영향이
    /// 빨리 빠져 그 저점을 하단 터치로 잡는다. 20-하단 OR N-하단 터치를 셋업으로 인정.
    /// 실측(KODEX레버 07-03 09:58 저점): 20-하단 −239원 차로 미터치 → 10-하단은 터치. 기본 10.
    /// </summary>
    public int BottomShortBandPeriod { get; set; } = 10;
    /// <summary>
    /// 흔들림 주의 VWAP 깊은 약세 임계(%): GC/🚀 확인 시 종가가 세션 VWAP보다 이 이상 아래면(하락 추세
    /// 진행 중 · 아직 바닥 미확인) 알림에 "⚠ 흔들림 주의" 병기. 실측(14일 GC): VWAP −3%↓ 진입은
    /// 도달률은 비슷해도 평균 저점 −2.2%(VWAP 위는 −0.6%)로 진입 후 흔들림이 커 버티기 어렵다.
    /// 강등은 아님(표기만) — 폭락 후 저점 확인 반등은 VWAP 근처로 올라와 있어 대부분 제외. 0=끔. 기본 3.
    /// (검증: 저점比 과도는 위험 신호가 아니었음 — 오히려 높을수록 도달률↑ → 이전 저점比 기준 폐기.)
    /// </summary>
    public double BottomChaseVwapBelowPct { get; set; } = 3;
    /// <summary>
    /// 🚀 진입 적기 알림에 표기할 권장 손절선(%). 일반 진입 기준. 실측(승리의 도달 전 최저 낙폭):
    /// 손절 −2%면 결국 오를 것의 92~94% 보존. 기본 2.
    /// </summary>
    public double BottomStopLossPct { get; set; } = 2;
    /// <summary>⚠ 흔들림 주의 진입의 권장 손절선(%). 흔들림 주의는 더 깊이 빠진 뒤 반등(90%지점 −3%) → 기본 3.</summary>
    public double BottomStopLossChasePct { get; set; } = 3;

    // ── 고점 경고 시그널(관심 종목 · 국내 1분봉 · 바닥의 거울상) ──
    /// <summary>셋업으로 인정할 RSI(14) 과매수 하한. 상단 터치 구간 최고 RSI가 이 값 이상일 때만 시그널.</summary>
    public double TopRsiMin { get; set; } = 70;
    /// <summary>트리거 봉 종가의 최대 %b. 이 이하로 밴드 안 복귀 마감해야 시그널.</summary>
    public double TopMaxPercentB { get; set; } = 0.8;
    /// <summary>거래량 클라이맥스 배수(20봉 평균 대비). 긴 윗꼬리와 함께 소진 증거 중 하나.</summary>
    public double TopVolumeRatio { get; set; } = 1.5;
    /// <summary>볼린저 상단 터치를 찾는 최근 완성봉 수.</summary>
    public int TopTouchLookback { get; set; } = 5;
    /// <summary>상단 밴드워킹으로 인정할 최소 터치 봉 수(단발 터치 제외).</summary>
    public int TopMinWalkTouches { get; set; } = 2;
    /// <summary>1차 경고 후 MA5/MA20 데드크로스 확인(2차) 알림 사용.</summary>
    public bool TopConfirmCross { get; set; } = true;
    /// <summary>데드크로스 확인 대기 시간(분).</summary>
    public int TopConfirmWindowMinutes { get; set; } = 20;
    /// <summary>같은 종목 고점 경고 재알림 쿨다운(분).</summary>
    public int TopCooldownMinutes { get; set; } = 15;

    /// <summary>켜면 한국 종목 상승/하락(추세) 알림을 프리(08:00)·정규(09:00) 개장 직후 N분간 음소거. 기본 꺼짐.</summary>
    public bool MuteKrOpenAlerts { get; set; } = false;
    /// <summary>개장 직후 음소거 구간(분). 기본 10.</summary>
    public int KrOpenMuteMinutes { get; set; } = 10;

    // ── 캐시된 OAuth 토큰(만료 전까지 재사용) ──
    public string CachedToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; } = DateTime.MinValue;

    [JsonIgnore] public bool HasKisCredentials => !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(AppSecret);

    // ──────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Stock.Catch");

    /// <summary>구 앱 이름(Stock.Fetch) 시절 설정 폴더 — 최초 실행 시 여기서 마이그레이션.</summary>
    private static string LegacyConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Stock.Fetch");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        MigrateLegacyConfig();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null) return cfg;
            }
        }
        catch { /* 손상된 설정은 기본값으로 폴백 */ }

        return new AppConfig();
    }

    /// <summary>
    /// Stock.Fetch → Stock.Catch 리네이밍 마이그레이션: 새 설정이 없고 구 폴더가 있으면
    /// 파일을 복사한다(구 폴더는 남겨둠 — 구버전 병행 실행 안전).
    /// </summary>
    private static void MigrateLegacyConfig()
    {
        try
        {
            if (File.Exists(ConfigPath) || !Directory.Exists(LegacyConfigDir)) return;
            Directory.CreateDirectory(ConfigDir);
            foreach (var src in Directory.GetFiles(LegacyConfigDir))
            {
                string dst = Path.Combine(ConfigDir, Path.GetFileName(src));
                if (!File.Exists(dst)) File.Copy(src, dst);
            }
        }
        catch { /* 마이그레이션 실패 시 새 설정으로 시작 */ }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }
}
