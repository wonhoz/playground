using Stock.Fetch.Models;

namespace Stock.Fetch.Indicators;

/// <summary>
/// 캔들 시퀀스로부터 차트에 필요한 지표 시리즈(이동평균·볼린저·RSI·거래량MA)를 계산해 보관한다.
/// 모든 배열은 <see cref="Candles"/>와 동일 인덱스로 정렬된다(부족 구간 NaN).
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

    public IndicatorSet(IReadOnlyList<Candle> candles, int bollPeriod = 20, double bollK = 2.0, int rsiPeriod = 14)
    {
        Candles = candles;

        var closes = candles.Select(c => (double)c.Close).ToList();
        var volumes = candles.Select(c => (double)c.Volume).ToList();

        Sma5 = IndicatorMath.Sma(closes, 5);
        Sma20 = IndicatorMath.Sma(closes, 20);
        Sma60 = IndicatorMath.Sma(closes, 60);
        (BollUpper, BollMiddle, BollLower) = IndicatorMath.Bollinger(closes, bollPeriod, bollK);
        Rsi14 = IndicatorMath.Rsi(closes, rsiPeriod);
        VolumeMa20 = IndicatorMath.Sma(volumes, 20);
    }
}
