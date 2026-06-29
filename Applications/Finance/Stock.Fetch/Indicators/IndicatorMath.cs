namespace Stock.Fetch.Indicators;

/// <summary>
/// 순수 지표 계산 함수 모음. 입력 길이와 동일한 길이의 배열을 반환하며,
/// 계산에 필요한 데이터가 부족한 선두 구간은 <see cref="double.NaN"/>으로 채운다.
/// (Stock.Watch에서 포팅)
/// </summary>
public static class IndicatorMath
{
    /// <summary>단순이동평균(SMA).</summary>
    public static double[] Sma(IReadOnlyList<double> values, int period)
    {
        var result = new double[values.Count];
        double sum = 0;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= period) sum -= values[i - period];
            result[i] = i >= period - 1 ? sum / period : double.NaN;
        }
        return result;
    }

    /// <summary>지수이동평균(EMA).</summary>
    public static double[] Ema(IReadOnlyList<double> values, int period)
    {
        var result = new double[values.Count];
        double k = 2.0 / (period + 1);
        double ema = 0;
        bool seeded = false;
        for (int i = 0; i < values.Count; i++)
        {
            if (!seeded)
            {
                // 첫 EMA 시드는 첫 period 구간의 SMA
                if (i < period - 1) { result[i] = double.NaN; continue; }
                double sum = 0;
                for (int j = i - period + 1; j <= i; j++) sum += values[j];
                ema = sum / period;
                seeded = true;
            }
            else
            {
                ema = values[i] * k + ema * (1 - k);
            }
            result[i] = ema;
        }
        return result;
    }

    /// <summary>RSI(Wilder 평활). 0~100.</summary>
    public static double[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        var result = new double[closes.Count];
        if (closes.Count == 0) return result;
        result[0] = double.NaN;

        double avgGain = 0, avgLoss = 0;
        for (int i = 1; i < closes.Count; i++)
        {
            double change = closes[i] - closes[i - 1];
            double gain = Math.Max(change, 0);
            double loss = Math.Max(-change, 0);

            if (i < period)
            {
                avgGain += gain;
                avgLoss += loss;
                result[i] = double.NaN;
                if (i == period - 1)
                {
                    avgGain /= period;
                    avgLoss /= period;
                }
                continue;
            }

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            double rs = avgLoss == 0 ? double.PositiveInfinity : avgGain / avgLoss;
            result[i] = avgLoss == 0 ? 100 : 100 - 100 / (1 + rs);
        }
        return result;
    }

    /// <summary>볼린저 밴드(중심선 SMA, 상·하단 ±k·표준편차).</summary>
    public static (double[] Upper, double[] Middle, double[] Lower) Bollinger(
        IReadOnlyList<double> closes, int period = 20, double k = 2.0)
    {
        var mid = Sma(closes, period);
        var upper = new double[closes.Count];
        var lower = new double[closes.Count];
        for (int i = 0; i < closes.Count; i++)
        {
            if (double.IsNaN(mid[i])) { upper[i] = lower[i] = double.NaN; continue; }
            double sumSq = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                double d = closes[j] - mid[i];
                sumSq += d * d;
            }
            double sd = Math.Sqrt(sumSq / period);
            upper[i] = mid[i] + k * sd;
            lower[i] = mid[i] - k * sd;
        }
        return (upper, mid, lower);
    }
}
