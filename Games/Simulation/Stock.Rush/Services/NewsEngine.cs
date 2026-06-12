namespace StockRush.Services;

/// <summary>
/// 뉴스 생성 엔진. 일정 간격으로 종목/섹터/시장 뉴스를 만들어 가격에 충격을 가한다.
/// 일부 뉴스는 페이크(영향 미미·반대 방향)로 긴장감을 유발.
/// </summary>
public class NewsEngine
{
    private readonly MarketEngine _engine;
    private readonly Random _rng;
    private int _nextNewsTick;

    public event Action<NewsItem>? NewsPublished;

    public NewsEngine(MarketEngine engine)
    {
        _engine = engine;
        _rng = engine.Rng;
        ScheduleNext(120);
    }

    private static readonly string[] GoodTemplates =
    {
        "{0}, 분기 영업이익 시장 전망치 크게 상회",
        "{0}, 대규모 공급 계약 체결 공시",
        "외국인·기관, {0} 동반 순매수 포착",
        "{0}, 자사주 매입·소각 결정",
        "{0} 신제품 발표에 증권가 호평 잇따라",
        "{0}, 해외 시장 진출 본격화",
        "증권사들, {0} 목표주가 일제히 상향",
    };

    private static readonly string[] BadTemplates =
    {
        "{0}, 분기 실적 쇼크... 영업이익 급감",
        "{0} 주요 공장 가동 중단 소식",
        "금융당국, {0} 회계 처리 적정성 점검 착수",
        "{0} 임원진, 보유 지분 대량 매도",
        "{0}, 핵심 계약 해지 통보받아",
        "경쟁사 약진에 {0} 점유율 하락 우려",
        "{0}, 신사업 투자 지연 공시",
    };

    private static readonly string[] NeutralTemplates =
    {
        "증권가, {0} 목표주가 유지... \"관망 권고\"",
        "{0}, 정기 주주총회 일정 공고",
        "{0} 거래량 평소 대비 급증... 배경 주목",
    };

    private static readonly string[] BreakingGood =
    {
        "[단독] {0}, 글로벌 빅테크와 인수합병 논의 보도",
        "{0}, 혁신 기술 개발 성공 발표... 업계 판도 변화 예고",
        "{0}, 사상 최대 규모 수주 잭팟",
    };

    private static readonly string[] BreakingBad =
    {
        "[단독] {0}, 핵심 임상 3상 실패 발표",
        "{0} 대규모 리콜 사태 발생... 손실 불가피",
        "검찰, {0} 본사 전격 압수수색",
    };

    private static readonly (string headline, double impact)[] MarketNews =
    {
        ("미 증시 급락 여파... 국내 증시 동반 약세", -0.020),
        ("미 증시 사상 최고치 경신... 위험자산 선호 확산", 0.018),
        ("한국은행, 기준금리 전격 인하", 0.022),
        ("한국은행, 기준금리 깜짝 인상... 긴축 우려", -0.022),
        ("원/달러 환율 급등... 외국인 자금 이탈 우려", -0.015),
        ("외국인 대규모 순매수 전환... 수급 개선 기대", 0.015),
        ("글로벌 경기 침체 경고음... 안전자산 쏠림", -0.018),
        ("대규모 경기 부양책 발표... 증시 환호", 0.020),
    };

    private static readonly Dictionary<Sector, (string good, string bad)> SectorNews = new()
    {
        [Sector.반도체] = ("반도체 업황 회복 기대감 확산... 수출 호조", "반도체 가격 하락 전망에 업종 전반 약세"),
        [Sector.바이오] = ("바이오 신약 기대감에 제약주 동반 강세", "바이오 거품 경고... 임상 리스크 부각"),
        [Sector.이차전지] = ("전기차 판매 급증... 배터리 수요 폭발 전망", "전기차 수요 둔화 우려에 배터리주 약세"),
        [Sector.게임] = ("신작 흥행 기대감에 게임주 들썩", "게임 규제 강화 움직임에 업종 긴장"),
        [Sector.금융] = ("금리 환경 개선에 금융주 수혜 기대", "부실채권 증가 우려... 금융주 약세"),
        [Sector.화학] = ("국제 유가 안정에 화학 업종 마진 개선", "원자재 가격 급등... 화학 업종 원가 부담"),
        [Sector.중공업] = ("조선 수주 랠리 지속... 중공업 슈퍼사이클", "글로벌 발주 감소에 중공업 전망 하향"),
        [Sector.엔터] = ("K-콘텐츠 글로벌 흥행... 엔터주 강세", "소속 아티스트 리스크에 엔터주 출렁"),
        [Sector.유통] = ("소비 심리 회복 조짐... 유통주 기대감", "소비 위축 지표에 유통 업종 부진"),
        [Sector.통신] = ("통신 요금제 개편 수혜 기대", "통신 요금 인하 압박에 수익성 우려"),
    };

    private void ScheduleNext(int baseDelay = 0)
    {
        // 평균 30~80초 (실시간) 간격 = 300~800틱
        var delay = baseDelay > 0 ? baseDelay : 300 + _rng.Next(0, 500);
        _nextNewsTick = _engine.TickCount + delay;
    }

    /// <summary>매 틱 호출 — 예약 시각 도달 시 뉴스 발행</summary>
    public void Tick()
    {
        if (!_engine.SessionOpen) return;
        if (_engine.TickCount < _nextNewsTick) return;
        PublishRandom();
        ScheduleNext();
    }

    private void PublishRandom()
    {
        var roll = _rng.NextDouble();
        if (roll < 0.12) PublishMarket();
        else if (roll < 0.30) PublishSector();
        else if (roll < 0.42) PublishBreaking();
        else PublishStock();
    }

    private void PublishMarket()
    {
        var (headline, impact) = MarketNews[_rng.Next(MarketNews.Length)];
        _engine.ApplyMarketShock(impact * (0.7 + _rng.NextDouble() * 0.8), 200 + _rng.Next(200));
        Publish(new NewsItem { Headline = headline, Kind = NewsKind.시장 });
    }

    private void PublishSector()
    {
        var sector = (Sector)_rng.Next(Enum.GetValues<Sector>().Length);
        var (good, bad) = SectorNews[sector];
        var isGood = _rng.NextDouble() < 0.5;
        var impact = (0.02 + _rng.NextDouble() * 0.03) * (isGood ? 1 : -1);
        _engine.ApplySectorShock(sector, impact, 200 + _rng.Next(200));
        Publish(new NewsItem
        {
            Headline = isGood ? good : bad,
            Kind = isGood ? NewsKind.호재 : NewsKind.악재,
            TargetSector = sector
        });
    }

    private void PublishStock()
    {
        var stock = _engine.Stocks[_rng.Next(_engine.Stocks.Count)];
        var roll = _rng.NextDouble();

        if (roll < 0.15)
        {
            // 중립(페이크) — 헤드라인만 요란하고 영향 거의 없음
            var tpl = NeutralTemplates[_rng.Next(NeutralTemplates.Length)];
            _engine.ApplyShock(stock, _engine.Gauss() * 0.005, 100);
            Publish(new NewsItem { Headline = string.Format(tpl, stock.Name), Kind = NewsKind.중립, TargetCode = stock.Code });
            return;
        }

        var isGood = roll < 0.575;
        var tpl2 = isGood ? GoodTemplates[_rng.Next(GoodTemplates.Length)] : BadTemplates[_rng.Next(BadTemplates.Length)];
        var impact = (0.03 + _rng.NextDouble() * 0.06) * (isGood ? 1 : -1);

        // 10% 확률 '소문에 사서 뉴스에 팔아라' — 호재인데 되려 하락 (또는 반대)
        if (_rng.NextDouble() < 0.10) impact *= -0.5;

        _engine.ApplyShock(stock, impact, 150 + _rng.Next(250));
        Publish(new NewsItem
        {
            Headline = string.Format(tpl2, stock.Name),
            Kind = isGood ? NewsKind.호재 : NewsKind.악재,
            TargetCode = stock.Code
        });
    }

    private void PublishBreaking()
    {
        var stock = _engine.Stocks[_rng.Next(_engine.Stocks.Count)];
        var isGood = _rng.NextDouble() < 0.5;
        var tpl = isGood ? BreakingGood[_rng.Next(BreakingGood.Length)] : BreakingBad[_rng.Next(BreakingBad.Length)];
        var gap = (0.04 + _rng.NextDouble() * 0.05) * (isGood ? 1 : -1);
        var drift = (0.06 + _rng.NextDouble() * 0.10) * (isGood ? 1 : -1);
        _engine.ApplyShock(stock, drift, 250 + _rng.Next(250), gap);
        Publish(new NewsItem
        {
            Headline = string.Format(tpl, stock.Name),
            Kind = isGood ? NewsKind.속보호재 : NewsKind.속보악재,
            TargetCode = stock.Code
        });
    }

    /// <summary>튜토리얼 등에서 임의 뉴스 직접 발행</summary>
    public void PublishScripted(NewsItem item) => Publish(item);

    private void Publish(NewsItem item)
    {
        item.Day = _engine.Day;
        item.Time = _engine.MarketTime;
        NewsPublished?.Invoke(item);
    }
}
