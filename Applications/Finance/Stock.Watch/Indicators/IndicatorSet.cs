using Stock.Watch.Models;

namespace Stock.Watch.Indicators;

/// <summary>
/// 캔들 시퀀스로부터 차트·조건 평가에 필요한 모든 지표 시리즈를 한 번에 계산해 보관한다.
/// 모든 배열은 <see cref="Candles"/>와 동일한 인덱스로 정렬된다(부족 구간 NaN).
/// </summary>
public sealed class IndicatorSet
{
    public IReadOnlyList<Candle> Candles { get; }

    public double[] Sma5 { get; }
    public double[] Sma20 { get; }
    public double[] Sma60 { get; }
    public double[] BollUpper { get; }
    public double[] BollMiddle { get; }
    public double[] BollLower { get; }
    public double[] Rsi14 { get; }
    public double[] VolumeMa20 { get; }
    public double[] Macd { get; }
    public double[] MacdSignal { get; }
    public double[] MacdHistogram { get; }

    public IndicatorSet(IReadOnlyList<Candle> candles, BollingerSettings? boll = null, int rsiPeriod = 14)
    {
        Candles = candles;
        boll ??= BollingerSettings.Default;

        var closes = candles.Select(c => (double)c.Close).ToList();
        var volumes = candles.Select(c => (double)c.Volume).ToList();

        Sma5 = IndicatorMath.Sma(closes, 5);
        Sma20 = IndicatorMath.Sma(closes, 20);
        Sma60 = IndicatorMath.Sma(closes, 60);
        (BollUpper, BollMiddle, BollLower) = IndicatorMath.Bollinger(closes, boll.Period, boll.K);
        Rsi14 = IndicatorMath.Rsi(closes, rsiPeriod);
        VolumeMa20 = IndicatorMath.Sma(volumes, 20);
        (Macd, MacdSignal, MacdHistogram) = IndicatorMath.Macd(closes);
    }

    public int LastIndex => Candles.Count - 1;

    /// <summary>마지막 봉 기준 지표 스냅샷(조건 평가용). 데이터 부족 시 null.</summary>
    public IndicatorSnapshot? Latest()
    {
        if (Candles.Count == 0) return null;
        int i = LastIndex;
        int p = i - 1; // 직전 봉(크로스 판정용)
        return new IndicatorSnapshot
        {
            Index = i,
            Close = (double)Candles[i].Close,
            PrevClose = p >= 0 ? (double)Candles[p].Close : double.NaN,
            Volume = Candles[i].Volume,
            Sma5 = Sma5[i],
            Sma20 = Sma20[i],
            Sma60 = Sma60[i],
            PrevSma20 = p >= 0 ? Sma20[p] : double.NaN,
            BollUpper = BollUpper[i],
            BollMiddle = BollMiddle[i],
            BollLower = BollLower[i],
            Rsi14 = Rsi14[i],
            PrevRsi14 = p >= 0 ? Rsi14[p] : double.NaN,
            VolumeMa20 = VolumeMa20[i],
            Macd = Macd[i],
            MacdSignal = MacdSignal[i],
            PrevMacd = p >= 0 ? Macd[p] : double.NaN,
            PrevMacdSignal = p >= 0 ? MacdSignal[p] : double.NaN,
        };
    }
}

public sealed record BollingerSettings(int Period, double K)
{
    public static BollingerSettings Default { get; } = new(20, 2.0);
}

/// <summary>특정 봉 시점의 지표값 모음. 조건 평가의 입력.</summary>
public sealed class IndicatorSnapshot
{
    public int Index { get; init; }
    public double Close { get; init; }
    public double PrevClose { get; init; }
    public long Volume { get; init; }
    public double Sma5 { get; init; }
    public double Sma20 { get; init; }
    public double Sma60 { get; init; }
    public double PrevSma20 { get; init; }
    public double BollUpper { get; init; }
    public double BollMiddle { get; init; }
    public double BollLower { get; init; }
    public double Rsi14 { get; init; }
    public double PrevRsi14 { get; init; }
    public double VolumeMa20 { get; init; }
    public double Macd { get; init; }
    public double MacdSignal { get; init; }
    public double PrevMacd { get; init; }
    public double PrevMacdSignal { get; init; }
}
