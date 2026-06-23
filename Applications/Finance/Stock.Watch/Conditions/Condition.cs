using Stock.Watch.Indicators;

namespace Stock.Watch.Conditions;

/// <summary>조건의 피연산자(지표/가격/거래량) 종류.</summary>
public enum Operand
{
    Price,        // 현재가(종가)
    Rsi14,        // RSI(14)
    BollUpper,    // 볼린저 상단
    BollMiddle,   // 볼린저 중심(SMA20)
    BollLower,    // 볼린저 하단
    Volume,       // 당일 거래량
    VolumeMa20,   // 거래량 20봉 평균
    Sma5,         // 5봉 이동평균
    Sma20,        // 20봉 이동평균
    Sma60,        // 60봉 이동평균
    Macd,         // MACD 선
    MacdSignal    // MACD 시그널 선
}

/// <summary>비교 연산자.</summary>
public enum CompareOp
{
    LessThan,        // <
    LessOrEqual,     // <= (터치 포함)
    GreaterThan,     // >
    GreaterOrEqual,  // >= (터치 포함)
    CrossAbove,      // 직전 봉 이하 → 현재 봉 초과 (상향 돌파)
    CrossBelow       // 직전 봉 이상 → 현재 봉 미만 (하향 돌파)
}

/// <summary>우변 종류: 상수 또는 (지표 × 배수).</summary>
public enum RightKind
{
    Constant,   // 고정 숫자값
    Indicator   // 지표값 × Multiplier
}

/// <summary>
/// 단일 조건. 예) RSI &lt; 30, 종가 ≤ 볼린저하단, 거래량 ≥ 거래량MA20 × 2, 종가 SMA20 상향돌파.
/// </summary>
public sealed class Condition
{
    public Operand Left { get; set; } = Operand.Rsi14;
    public CompareOp Op { get; set; } = CompareOp.LessThan;
    public RightKind RightType { get; set; } = RightKind.Constant;

    /// <summary>RightType=Constant이면 비교값, Indicator이면 배수(Multiplier).</summary>
    public double RightValue { get; set; } = 30;

    /// <summary>RightType=Indicator일 때 비교 대상 지표.</summary>
    public Operand RightOperand { get; set; } = Operand.BollLower;

    /// <summary>지정 인덱스 시점에 조건이 참인지 평가. 데이터 부족 시 false.</summary>
    public bool Evaluate(IndicatorSet set, int index)
    {
        double left = Resolve(Left, set, index);
        double right = ResolveRight(set, index);
        if (double.IsNaN(left) || double.IsNaN(right)) return false;

        switch (Op)
        {
            case CompareOp.LessThan: return left < right;
            case CompareOp.LessOrEqual: return left <= right;
            case CompareOp.GreaterThan: return left > right;
            case CompareOp.GreaterOrEqual: return left >= right;
            case CompareOp.CrossAbove:
            case CompareOp.CrossBelow:
                if (index < 1) return false;
                double prevLeft = Resolve(Left, set, index - 1);
                double prevRight = ResolveRight(set, index - 1);
                if (double.IsNaN(prevLeft) || double.IsNaN(prevRight)) return false;
                return Op == CompareOp.CrossAbove
                    ? prevLeft <= prevRight && left > right
                    : prevLeft >= prevRight && left < right;
            default: return false;
        }
    }

    private double ResolveRight(IndicatorSet set, int index)
        => RightType == RightKind.Constant
            ? RightValue
            : Resolve(RightOperand, set, index) * RightValue;

    private static double Resolve(Operand op, IndicatorSet set, int i) => op switch
    {
        Operand.Price => (double)set.Candles[i].Close,
        Operand.Rsi14 => set.Rsi14[i],
        Operand.BollUpper => set.BollUpper[i],
        Operand.BollMiddle => set.BollMiddle[i],
        Operand.BollLower => set.BollLower[i],
        Operand.Volume => set.Candles[i].Volume,
        Operand.VolumeMa20 => set.VolumeMa20[i],
        Operand.Sma5 => set.Sma5[i],
        Operand.Sma20 => set.Sma20[i],
        Operand.Sma60 => set.Sma60[i],
        Operand.Macd => set.Macd[i],
        Operand.MacdSignal => set.MacdSignal[i],
        _ => double.NaN
    };

    /// <summary>UI·Slack 표시용 한글 요약. 예) "RSI &lt; 30".</summary>
    public string Summary()
    {
        string l = OperandLabel(Left);
        string o = OpLabel(Op);
        string r = RightType == RightKind.Constant
            ? FormatNumber(RightValue)
            : (RightValue == 1 ? OperandLabel(RightOperand) : $"{OperandLabel(RightOperand)} × {FormatNumber(RightValue)}");
        return $"{l} {o} {r}";
    }

    private static string FormatNumber(double v)
        => v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("0.###");

    public static string OperandLabel(Operand op) => op switch
    {
        Operand.Price => "현재가",
        Operand.Rsi14 => "RSI",
        Operand.BollUpper => "볼린저상단",
        Operand.BollMiddle => "볼린저중심",
        Operand.BollLower => "볼린저하단",
        Operand.Volume => "거래량",
        Operand.VolumeMa20 => "거래량MA20",
        Operand.Sma5 => "SMA5",
        Operand.Sma20 => "SMA20",
        Operand.Sma60 => "SMA60",
        Operand.Macd => "MACD",
        Operand.MacdSignal => "MACD시그널",
        _ => op.ToString()
    };

    public static string OpLabel(CompareOp op) => op switch
    {
        CompareOp.LessThan => "<",
        CompareOp.LessOrEqual => "≤",
        CompareOp.GreaterThan => ">",
        CompareOp.GreaterOrEqual => "≥",
        CompareOp.CrossAbove => "상향돌파",
        CompareOp.CrossBelow => "하향돌파",
        _ => op.ToString()
    };
}
