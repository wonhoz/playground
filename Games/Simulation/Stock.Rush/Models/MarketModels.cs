namespace StockRush.Models;

public enum Sector
{
    반도체, 바이오, 이차전지, 게임, 금융, 화학, 중공업, 엔터, 유통, 통신
}

/// <summary>분봉 캔들 (5분봉)</summary>
public class Candle
{
    public TimeSpan Time { get; set; }
    public long Open { get; set; }
    public long High { get; set; }
    public long Low { get; set; }
    public long Close { get; set; }
    public long Volume { get; set; }
}

public class Stock : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public Sector Sector { get; init; }

    /// <summary>틱당 기본 변동성 (랜덤워크 표준편차)</summary>
    public double Volatility { get; init; }

    /// <summary>내부 연속 가격 (호가단위 반올림 전)</summary>
    public double RawPrice { get; set; }

    /// <summary>전일 종가 (상하한가 기준)</summary>
    public long PrevClose { get; set; }
    public long DayOpen { get; set; }
    public long DayHigh { get; set; }
    public long DayLow { get; set; }

    /// <summary>뉴스로 인한 틱당 드리프트</summary>
    public double NewsDrift { get; set; }
    public int NewsDriftTicks { get; set; }

    /// <summary>VI 발동 잔여 틱 (0이면 정상 거래)</summary>
    public int HaltTicks { get; set; }

    /// <summary>최근 가격 이력 (VI 판정용)</summary>
    public Queue<long> RecentPrices { get; } = new();

    public List<Candle> Candles { get; } = new();
    public Candle? CurrentCandle { get; set; }

    private long _price;
    public long Price
    {
        get => _price;
        set { if (_price != value) { _price = value; OnChanged(nameof(Price)); OnChanged(nameof(PriceText)); } }
    }

    private long _accVolume;
    public long AccVolume
    {
        get => _accVolume;
        set { if (_accVolume != value) { _accVolume = value; OnChanged(nameof(VolumeText)); } }
    }

    public long Change => Price - PrevClose;
    public double ChangeRate => PrevClose == 0 ? 0 : (double)Change / PrevClose * 100.0;

    public string PriceText => Price.ToString("N0");
    public string ChangeText => $"{(Change >= 0 ? "▲" : "▼")} {Math.Abs(Change):N0}";
    public string ChangeRateText => $"{(ChangeRate >= 0 ? "+" : "")}{ChangeRate:F2}%";
    public string VolumeText => AccVolume >= 1_000_000 ? $"{AccVolume / 1_000_000.0:F1}M" : AccVolume.ToString("N0");

    public bool IsUpperLimit => Price >= MarketEngineRef.UpperLimit(PrevClose);
    public bool IsLowerLimit => Price <= MarketEngineRef.LowerLimit(PrevClose);

    private Brush _trendBrush = Brushes.Gray;
    public Brush TrendBrush
    {
        get => _trendBrush;
        set { if (!Equals(_trendBrush, value)) { _trendBrush = value; OnChanged(nameof(TrendBrush)); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnChanged(nameof(IsSelected)); } }
    }

    private string _stateText = "";
    /// <summary>VI / 상한 / 하한 상태 뱃지</summary>
    public string StateText
    {
        get => _stateText;
        set { if (_stateText != value) { _stateText = value; OnChanged(nameof(StateText)); } }
    }

    public void RefreshDerived()
    {
        OnChanged(nameof(ChangeText));
        OnChanged(nameof(ChangeRateText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Stock에서 엔진 정적 함수 참조용 (순환 의존 회피)</summary>
public static class MarketEngineRef
{
    public static long UpperLimit(long prevClose) => Services.MarketEngine.RoundToTick(prevClose * 1.3, false);
    public static long LowerLimit(long prevClose) => Services.MarketEngine.RoundToTick(prevClose * 0.7, true);
}

public enum NewsKind { 호재, 악재, 중립, 속보호재, 속보악재, 시장 }

public class NewsItem
{
    public TimeSpan Time { get; set; }
    public int Day { get; set; }
    public string Headline { get; set; } = "";
    public NewsKind Kind { get; set; }
    public string? TargetCode { get; set; }
    public Sector? TargetSector { get; set; }

    public string TimeText => $"{Time.Hours:D2}:{Time.Minutes:D2}";
    public bool IsBreaking => Kind is NewsKind.속보호재 or NewsKind.속보악재;
    public string KindBadge => Kind switch
    {
        NewsKind.속보호재 or NewsKind.속보악재 => "속보",
        NewsKind.시장 => "시장",
        _ => "뉴스"
    };
    public Brush KindBrush => Kind switch
    {
        NewsKind.속보호재 or NewsKind.속보악재 => new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00)),
        NewsKind.시장 => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xB8)),
        _ => new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x88))
    };
}

/// <summary>호가창 한 줄 (매도호가는 위쪽, 매수호가는 아래쪽)</summary>
public class BookRow : INotifyPropertyChanged
{
    public bool IsAsk { get; init; }

    private long _price;
    public long Price
    {
        get => _price;
        set { if (_price != value) { _price = value; OnChanged(nameof(PriceText)); OnChanged(nameof(RateText)); } }
    }

    private long _qty;
    public long Qty
    {
        get => _qty;
        set { if (_qty != value) { _qty = value; OnChanged(nameof(QtyText)); OnChanged(nameof(BarWidth)); } }
    }

    /// <summary>등락률 표시 기준 전일종가</summary>
    public long PrevClose { get; set; }
    /// <summary>잔량 막대 최대 기준</summary>
    public long MaxQty { get; set; } = 1;

    public string PriceText => Price > 0 ? Price.ToString("N0") : "";
    public string QtyText => Qty > 0 ? Qty.ToString("N0") : "";
    public double BarWidth => Qty <= 0 ? 0 : Math.Min(90.0, 90.0 * Qty / Math.Max(1, MaxQty));
    public string RateText
    {
        get
        {
            if (Price <= 0 || PrevClose <= 0) return "";
            var r = (double)(Price - PrevClose) / PrevClose * 100.0;
            return $"{(r >= 0 ? "+" : "")}{r:F2}%";
        }
    }
    public Brush RateBrush
    {
        get
        {
            if (Price <= 0 || PrevClose <= 0) return Brushes.Gray;
            return Price >= PrevClose ? Ui.UpBrush : Ui.DownBrush;
        }
    }

    public void Refresh()
    {
        OnChanged(nameof(PriceText)); OnChanged(nameof(QtyText));
        OnChanged(nameof(BarWidth)); OnChanged(nameof(RateText)); OnChanged(nameof(RateBrush));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>공용 색상 (한국식: 상승 빨강 / 하락 파랑)</summary>
public static class Ui
{
    public static readonly Brush UpBrush = Frozen(Color.FromRgb(0xF0, 0x44, 0x52));
    public static readonly Brush DownBrush = Frozen(Color.FromRgb(0x31, 0x82, 0xF6));
    public static readonly Brush FlatBrush = Frozen(Color.FromRgb(0xE0, 0xE0, 0xE0));
    public static readonly Brush DimBrush = Frozen(Color.FromRgb(0x88, 0x88, 0x88));
    public static readonly Brush WarnBrush = Frozen(Color.FromRgb(0xFF, 0xB0, 0x00));

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static Brush ForChange(long change) => change > 0 ? UpBrush : change < 0 ? DownBrush : FlatBrush;
    public static Brush ForChange(double change) => change > 0 ? UpBrush : change < 0 ? DownBrush : FlatBrush;
}
