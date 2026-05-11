# Quant.Lab

KRX 일봉 OHLCV CSV를 입력으로 받아 단일 종목 단일 전략 백테스트를 수행하고, 콘솔 요약 + HTML 리포트를 생성하는 미니멀 백테스트 프레임워크.

> **주의**: 학습·검증·실험용 도구입니다. 투자 판단·매매 권유가 아닙니다.

## 구조

| 프로젝트 | 역할 |
|---------|-----|
| `Quant.Lab.Core` | 라이브러리 — 데이터 로더 / 전략 인터페이스 / 백테스트 엔진 / 메트릭 / HTML 리포트 |
| `Quant.Lab.Cli`  | 콘솔 실행기 (`Quant.Lab.exe`) |

## 빌드

```bash
dotnet build Applications/Finance/Quant.Lab/Quant.Lab.Cli/Quant.Lab.Cli.csproj -c Release
```

## 사용 예

샘플 CSV (240영업일 가상 데이터) 포함:

```bash
dotnet run --project Applications/Finance/Quant.Lab/Quant.Lab.Cli -- \
  --csv=Applications/Finance/Quant.Lab/samples/005930_sample.csv \
  --short=5 --long=20 --cash=10000000 \
  --report=report.html
```

### CLI 옵션

| 옵션 | 기본값 | 설명 |
|------|-------|------|
| `--csv=<path>` | (필수) | OHLCV CSV 경로 |
| `--strategy=<name>` | `sma` | 전략명 (현재 `sma`만 지원) |
| `--short=<int>` | `5` | SMA 단기 기간 |
| `--long=<int>` | `20` | SMA 장기 기간 |
| `--cash=<decimal>` | `10000000` | 초기 자본 (원) |
| `--commission=<rate>` | `0.00015` | 매수/매도 수수료율 (0.015%) |
| `--tax=<rate>` | `0.0023` | 매도 거래세율 (0.23%) |
| `--slippage=<rate>` | `0.0005` | 슬리피지율 (0.05%) |
| `--report=<path>` | (없음) | HTML 리포트 출력 경로 |

## CSV 형식

헤더 또는 무헤더 모두 지원. 헤더 사용 시 컬럼명: `Date, Open, High, Low, Close, Volume`.
- 날짜: `yyyy-MM-dd`, `yyyy/MM/dd`, `yyyy.MM.dd`, `yyyyMMdd`
- 천단위 콤마 자동 제거
- BOM 자동 감지

```csv
Date,Open,High,Low,Close,Volume
2024-09-02,70000,70236,69925,70151,5136586
2024-09-03,70151,70309,69433,69868,5077537
...
```

## 엔진 동작

- **체결 시점**: 봉 종가 기준 시그널 → **다음 봉 시가** 체결 (look-ahead bias 방지)
- **수수료 / 거래세 / 슬리피지** 모델링
- **자산 곡선**은 매봉 종가 평가 기준
- **메트릭**: 총수익, CAGR, MDD, Sharpe (연 252영업일 기준), 거래 수, 승률, 평균 수익/손실 거래수익률
- **마지막 봉 강제 청산** (`LiquidateAtEnd = true` 기본)

## 라이브러리로 사용 (직접 호출)

```csharp
using Quant.Lab.Core.Data;
using Quant.Lab.Core.Engine;
using Quant.Lab.Core.Reporting;
using Quant.Lab.Core.Strategies;

var bars = new CsvDataLoader("samples/005930_sample.csv").Load();
var strategy = new SmaCrossoverStrategy(shortPeriod: 5, longPeriod: 20);
var engine = new BacktestEngine(new BacktestConfig(InitialCash: 10_000_000m));

var result = engine.Run(bars, strategy);

Console.WriteLine($"Total Return: {result.Metrics.TotalReturn:P2}");
HtmlReporter.Save(result, "report.html");
```

## 새 전략 추가

`IStrategy` 구현 → `OnBar(StrategyContext ctx)` 안에서 `ctx.BuyAll() / ctx.SellAll() / ctx.BuyMarket(qty)` 호출.

```csharp
public sealed class MyStrategy : IStrategy
{
    public string Name => "My Strategy";

    public void OnBar(StrategyContext ctx)
    {
        if (ctx.Index < 20) return;
        var sma = ...; // ctx.Bars 슬라이스로 계산
        if (조건 && ctx.Position == 0) ctx.BuyAll();
        else if (반대조건 && ctx.Position > 0) ctx.SellAll();
    }
}
```

## 향후 확장 후보

- 다종목 포트폴리오 / 워크포워드 / 옵티마이저 (그리드/랜덤서치)
- 추가 전략: RSI, Bollinger, Donchian, MACD
- KRX/네이버 시세 자동 다운로더 (`IDataLoader` 추가 구현체)
- 분봉·틱 지원

## 버전

- `1.0.0` — MVP (CSV 로더, SMA Crossover, 백테스트 엔진, HTML 리포트)
