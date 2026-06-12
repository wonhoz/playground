namespace StockRush.Services;

/// <summary>스크립트 가격 시퀀스 한 구간 (틱당 드리프트 × 지속 틱)</summary>
public class ScriptSegment
{
    public double Drift;
    public int Ticks;
}

public class TutorialContext
{
    public required MarketEngine Engine { get; init; }
    public required Account Account { get; init; }
    public required NewsEngine News { get; init; }
    public required Stock Target { get; init; }
    public required Func<Stock?> GetSelected { get; init; }

    public long EntryAvg;
    public int WaitUntilTick;
    public bool NewsFired;
    public bool ReversalFired;
    public int NewsFireTick;

    /// <summary>순차 실행되는 가격 스크립트 — 캔들 모양·추세를 정밀 연출</summary>
    public Queue<ScriptSegment> Script { get; } = new();
    public void AddScript(double driftPerTick, int ticks)
    {
        if (ticks > 0) Script.Enqueue(new ScriptSegment { Drift = driftPerTick, Ticks = ticks });
    }
}

public class TutorialStep
{
    public required string Text { get; init; }
    public required Func<TutorialContext, bool> IsComplete { get; init; }
    public Action<TutorialContext>? OnEnter { get; init; }
}

public class TutorialScenario
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required List<TutorialStep> Steps { get; init; }
    public required Func<TutorialContext, string> ResultMessage { get; init; }
}

/// <summary>
/// 튜토리얼 진행기. 시장은 평소대로 흐르되, 대상 종목에 스크립트 충격을 주입해
/// 정해진 상황(상승·속보·급락)을 연출하고 사용자의 행동을 검증한다.
/// </summary>
public class TutorialManager
{
    private TutorialScenario? _scenario;
    private TutorialContext? _ctx;
    private int _stepIndex;

    public bool IsActive => _scenario != null;
    public string ScenarioTitle => _scenario?.Title ?? "";
    public int StepNumber => _stepIndex + 1;
    public int StepTotal => _scenario?.Steps.Count ?? 0;

    public event Action<string>? StepChanged;
    public event Action<string>? Completed;

    public static readonly (string Id, string Title, string Desc)[] Catalog =
    {
        ("basic", "기초 매매", "시장가 매수 → 수익 실현 매도의 기본 흐름을 익힙니다."),
        ("news", "뉴스 트레이딩", "호재 속보에 빠르게 올라타고, 꺾이기 전에 빠져나오는 연습."),
        ("stoploss", "손절 연습", "악재 급락 시 손실을 -5% 이내로 끊어내는 훈련."),
        ("candle", "캔들·추세 읽기", "망치형 반전, 추세 추종 — 캔들 차트로 매매 타이밍 잡는 법."),
    };

    public void Start(string id, TutorialContext ctx)
    {
        _ctx = ctx;
        _scenario = Build(id, ctx);
        _stepIndex = 0;
        _scenario.Steps[0].OnEnter?.Invoke(ctx);
        StepChanged?.Invoke(_scenario.Steps[0].Text);
    }

    public void Stop()
    {
        _scenario = null;
        _ctx = null;
    }

    public void Tick()
    {
        if (_scenario == null || _ctx == null) return;

        // 예약된 스크립트 뉴스 발화
        if (_ctx.NewsFireTick > 0 && !_ctx.NewsFired && _ctx.Engine.TickCount >= _ctx.NewsFireTick)
        {
            _ctx.NewsFired = true;
            FireScriptedNews(_ctx);
        }

        // 가격 스크립트 시퀀스 실행 (다음 엔진 틱 1회분 드리프트 주입)
        if (_ctx.Script.Count > 0)
        {
            var seg = _ctx.Script.Peek();
            _ctx.Target.NewsDrift = seg.Drift;
            _ctx.Target.NewsDriftTicks = 1;
            if (--seg.Ticks <= 0) _ctx.Script.Dequeue();
        }

        // 뉴스 드리프트 종료 후 차익 매물 반전 (뉴스 트레이딩 시나리오)
        if (_scenario.Id == "news" && _ctx.NewsFired && !_ctx.ReversalFired && _ctx.Target.NewsDriftTicks == 0)
        {
            _ctx.ReversalFired = true;
            _ctx.Engine.ApplyShock(_ctx.Target, -0.06, 200);
            _ctx.News.PublishScripted(new NewsItem
            {
                Headline = $"{_ctx.Target.Name}, 급등 후 차익 실현 매물 출회",
                Kind = NewsKind.악재,
                TargetCode = _ctx.Target.Code
            });
        }

        var step = _scenario.Steps[_stepIndex];
        if (!step.IsComplete(_ctx)) return;

        _stepIndex++;
        if (_stepIndex >= _scenario.Steps.Count)
        {
            var msg = _scenario.ResultMessage(_ctx);
            var done = _scenario;
            Stop();
            _ = done;
            Completed?.Invoke(msg);
            return;
        }

        var next = _scenario.Steps[_stepIndex];
        next.OnEnter?.Invoke(_ctx);
        StepChanged?.Invoke(next.Text);
    }

    private void FireScriptedNews(TutorialContext ctx)
    {
        if (_scenario == null) return;
        if (_scenario.Id == "news")
        {
            ctx.Engine.ApplyShock(ctx.Target, 0.10, 250, 0.02);
            ctx.News.PublishScripted(new NewsItem
            {
                Headline = $"[단독] {ctx.Target.Name}, 사상 최대 규모 수주 잭팟",
                Kind = NewsKind.속보호재,
                TargetCode = ctx.Target.Code
            });
        }
        else if (_scenario.Id == "stoploss")
        {
            ctx.Engine.ApplyShock(ctx.Target, -0.15, 200, -0.03);
            ctx.News.PublishScripted(new NewsItem
            {
                Headline = $"[단독] {ctx.Target.Name}, 대규모 계약 파기 통보... 실적 직격탄",
                Kind = NewsKind.속보악재,
                TargetCode = ctx.Target.Code
            });
        }
    }

    private static TutorialScenario Build(string id, TutorialContext ctx) => id switch
    {
        "basic" => new TutorialScenario
        {
            Id = "basic",
            Title = "기초 매매",
            Steps = new List<TutorialStep>
            {
                new()
                {
                    Text = $"왼쪽 관심종목 목록에서 [{ctx.Target.Name}]을(를) 클릭해 선택하세요.",
                    IsComplete = c => c.GetSelected()?.Code == c.Target.Code
                },
                new()
                {
                    Text = "주문 패널에서 수량 10주를 입력하고 빨간 [매수] 버튼을 누르세요. (시장가 주문)",
                    IsComplete = c => (c.Account.GetPosition(c.Target.Code)?.Qty ?? 0) >= 10
                },
                new()
                {
                    Text = "매수 완료! 오른쪽 잔고에 평가손익이 실시간으로 움직입니다. 수익률 +2% 도달까지 기다려 보세요.",
                    OnEnter = c =>
                    {
                        c.EntryAvg = c.Account.GetPosition(c.Target.Code)?.AvgPrice ?? c.Target.Price;
                        c.Engine.ApplyShock(c.Target, 0.035, 200);
                    },
                    IsComplete = c => c.Target.Price >= (long)(c.EntryAvg * 1.02)
                },
                new()
                {
                    Text = "+2% 도달! 이제 파란 [매도] 버튼으로 전량 매도해 수익을 확정하세요. (수량은 [최대]로 채우면 편합니다)",
                    IsComplete = c => c.Account.GetPosition(c.Target.Code) == null
                },
            },
            ResultMessage = c =>
            {
                var realized = c.Account.Trades.FirstOrDefault(t => t.Side == OrderSide.매도)?.RealizedPnl ?? 0;
                return $"기초 매매 완료!\n\n실현손익: {(realized >= 0 ? "+" : "")}{realized:N0}원\n\n" +
                       "매도 금액에서는 수수료 0.015% + 거래세 0.2%가 차감됩니다.\n" +
                       "수익이 났는데 생각보다 적다면 — 그것이 바로 거래 비용입니다.";
            }
        },

        "news" => new TutorialScenario
        {
            Id = "news",
            Title = "뉴스 트레이딩",
            Steps = new List<TutorialStep>
            {
                new()
                {
                    Text = "잠시 후 특정 종목에 호재 속보가 뜹니다. 하단 뉴스 피드를 주시하세요...",
                    OnEnter = c => c.NewsFireTick = c.Engine.TickCount + 80,
                    IsComplete = c => c.NewsFired
                },
                new()
                {
                    Text = $"속보 발생! [{ctx.Target.Name}] 상승 초입입니다. 종목을 선택하고 10주 이상 매수하세요. 늦을수록 비싸집니다!",
                    IsComplete = c => (c.Account.GetPosition(c.Target.Code)?.Qty ?? 0) >= 10
                },
                new()
                {
                    Text = "올라탔습니다! 하지만 급등 뒤엔 차익 매물이 나옵니다. 상승세가 꺾이기 전에 전량 매도하세요. 욕심은 금물!",
                    OnEnter = c => c.EntryAvg = c.Account.GetPosition(c.Target.Code)?.AvgPrice ?? c.Target.Price,
                    IsComplete = c => c.Account.GetPosition(c.Target.Code) == null
                },
            },
            ResultMessage = c =>
            {
                var realized = c.Account.Trades.FirstOrDefault(t => t.Side == OrderSide.매도)?.RealizedPnl ?? 0;
                var grade = realized > 0 ? "수익 탈출 성공! 뉴스 트레이딩의 핵심은 '빠른 진입, 더 빠른 탈출'입니다."
                                         : "손실 탈출... 고점에서 욕심을 부리면 차익 매물에 물립니다. 다시 도전해 보세요.";
                return $"뉴스 트레이딩 완료!\n\n실현손익: {(realized >= 0 ? "+" : "")}{realized:N0}원\n\n{grade}";
            }
        },

        "stoploss" => new TutorialScenario
        {
            Id = "stoploss",
            Title = "손절 연습",
            Steps = new List<TutorialStep>
            {
                new()
                {
                    Text = $"[{ctx.Target.Name}] 20주를 자동 매수했습니다. 평온해 보이지만... 시장은 언제든 돌변합니다.",
                    OnEnter = c =>
                    {
                        c.Account.ExecuteBuy(c.Target, c.Target.Price, 20, c.Engine.Day, c.Engine.MarketTime);
                        c.EntryAvg = c.Account.GetPosition(c.Target.Code)?.AvgPrice ?? c.Target.Price;
                        c.WaitUntilTick = c.Engine.TickCount + 60;
                        c.NewsFireTick = c.Engine.TickCount + 70;
                    },
                    IsComplete = c => c.Engine.TickCount >= c.WaitUntilTick
                },
                new()
                {
                    Text = "악재 속보! 주가가 급락합니다. 손실이 -5%를 넘기 전에 전량 매도(손절)하세요. 망설일수록 손실이 커집니다!",
                    IsComplete = c => c.Account.GetPosition(c.Target.Code) == null
                },
            },
            ResultMessage = c =>
            {
                var trade = c.Account.Trades.FirstOrDefault(t => t.Side == OrderSide.매도);
                var lossRate = c.EntryAvg > 0 && trade != null
                    ? (double)(trade.Price - c.EntryAvg) / c.EntryAvg * 100.0 : 0;
                var grade = lossRate >= -5.0
                    ? $"손절 성공! ({lossRate:F2}%) 손절은 더 큰 손실을 막는 보험입니다."
                    : $"손절이 늦었습니다. ({lossRate:F2}%) '조금만 더 기다리면 오르겠지'가 계좌를 녹입니다.";
                return $"손절 연습 완료!\n\n{grade}";
            }
        },

        "candle" => new TutorialScenario
        {
            Id = "candle",
            Title = "캔들·추세 읽기",
            Steps = new List<TutorialStep>
            {
                new()
                {
                    Text = "캔들 1개 = 5분입니다. 빨간 양봉은 종가가 시가보다 높게(상승 마감), 파란 음봉은 낮게(하락 마감) 끝났다는 뜻. " +
                           "위아래 꼬리는 그 사이 닿았던 고가·저가입니다. 잠시 차트를 관찰하세요.",
                    OnEnter = c => c.WaitUntilTick = c.Engine.TickCount + 70,
                    IsComplete = c => c.Engine.TickCount >= c.WaitUntilTick
                },
                new()
                {
                    Text = "음봉이 이어지는 하락 추세가 시작됐습니다. 떨어지는 도중에 사는 것은 '떨어지는 칼날 잡기' — " +
                           "바닥 신호가 나올 때까지 절대 매수하지 말고 기다리세요.",
                    OnEnter = c => c.AddScript(-0.0004, 100),
                    IsComplete = c => c.Script.Count == 0
                },
                new()
                {
                    Text = "바닥권에서 긴 아래꼬리 캔들(망치형·Hammer)이 만들어집니다 — 급락을 매수세가 받아 올린 대표적 반전 신호입니다. " +
                           "꼬리가 완성되면 10주 이상 매수하세요!",
                    OnEnter = c =>
                    {
                        // 다음 캔들 시작(틱%30==1)에 맞춰 망치형 연출: 급락 후 회복 = 긴 아래꼬리
                        var pad = ((1 - c.Engine.TickCount % 30) + 30) % 30;
                        c.AddScript(0, pad);
                        c.AddScript(-0.0025, 12);
                        c.AddScript(0.003, 12);
                    },
                    IsComplete = c => (c.Account.GetPosition(c.Target.Code)?.Qty ?? 0) >= 10
                },
                new()
                {
                    Text = "반전 성공, 상승 추세입니다! 저점과 고점이 함께 높아지는 동안은 보유가 원칙. " +
                           "장대음봉이 나오며 추세가 꺾이기 시작하면 미련 없이 전량 매도하세요.",
                    OnEnter = c =>
                    {
                        c.EntryAvg = c.Account.GetPosition(c.Target.Code)?.AvgPrice ?? c.Target.Price;
                        c.AddScript(0.0003, 150);
                        c.AddScript(-0.0006, 120);
                    },
                    IsComplete = c => c.Account.GetPosition(c.Target.Code) == null
                },
            },
            ResultMessage = c =>
            {
                var realized = c.Account.Trades.FirstOrDefault(t => t.Side == OrderSide.매도)?.RealizedPnl ?? 0;
                var grade = realized > 0
                    ? "추세를 타고 수익 실현! 패턴이 보일 때까지 기다린 보상입니다."
                    : "손실 마감... 매수가 늦었거나 매도가 늦었습니다. 패턴 출현 직후의 속도가 생명입니다.";
                return $"캔들·추세 읽기 완료!\n\n실현손익: {(realized >= 0 ? "+" : "")}{realized:N0}원\n\n{grade}\n\n" +
                       "오늘 배운 것 — ① 양봉/음봉과 꼬리 읽기 ② 하락 추세엔 칼날 잡지 않기 " +
                       "③ 망치형 = 반전 신호 ④ 상승 추세는 꺾일 때까지 보유.\n" +
                       "이는 실제 기술적 분석(캔들스틱 차트 분석)의 기본기지만, 실전에서 패턴은 확률일 뿐 — 항상 손절 원칙과 함께 쓰세요.";
            }
        },

        _ => throw new ArgumentException($"알 수 없는 시나리오: {id}")
    };
}
