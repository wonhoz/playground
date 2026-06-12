namespace StockRush.Models;

public enum OrderSide { 매수, 매도 }
public enum OrderType { 시장가, 지정가 }
public enum OrderStatus { 대기, 체결, 취소 }

public class Order : INotifyPropertyChanged
{
    private static int _nextId = 1;
    public int Id { get; } = _nextId++;
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public OrderSide Side { get; init; }
    public OrderType Type { get; init; }
    public long LimitPrice { get; init; }
    public long Qty { get; init; }

    private OrderStatus _status = OrderStatus.대기;
    public OrderStatus Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnChanged(nameof(StatusText)); } }
    }

    public string SideText => Side.ToString();
    public Brush SideBrush => Side == OrderSide.매수 ? Ui.UpBrush : Ui.DownBrush;
    public string PriceText => LimitPrice.ToString("N0");
    public string QtyText => Qty.ToString("N0");
    public string StatusText => Status.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Position : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";

    private long _qty;
    public long Qty
    {
        get => _qty;
        set { if (_qty != value) { _qty = value; OnChanged(nameof(QtyText)); } }
    }

    private long _avgPrice;
    public long AvgPrice
    {
        get => _avgPrice;
        set { if (_avgPrice != value) { _avgPrice = value; OnChanged(nameof(AvgPriceText)); } }
    }

    private long _currentPrice;
    public long CurrentPrice
    {
        get => _currentPrice;
        set
        {
            if (_currentPrice != value)
            {
                _currentPrice = value;
                OnChanged(nameof(CurrentPriceText));
                OnChanged(nameof(PnlText));
                OnChanged(nameof(PnlRateText));
                OnChanged(nameof(PnlBrush));
            }
        }
    }

    public long Pnl => (CurrentPrice - AvgPrice) * Qty;
    public double PnlRate => AvgPrice == 0 ? 0 : (double)(CurrentPrice - AvgPrice) / AvgPrice * 100.0;

    public string QtyText => Qty.ToString("N0");
    public string AvgPriceText => AvgPrice.ToString("N0");
    public string CurrentPriceText => CurrentPrice.ToString("N0");
    public string PnlText => $"{(Pnl >= 0 ? "+" : "")}{Pnl:N0}";
    public string PnlRateText => $"{(PnlRate >= 0 ? "+" : "")}{PnlRate:F2}%";
    public Brush PnlBrush => Ui.ForChange(Pnl);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class TradeRecord
{
    public int Day { get; set; }
    public TimeSpan Time { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public OrderSide Side { get; set; }
    public long Price { get; set; }
    public long Qty { get; set; }
    /// <summary>매도 시 실현손익 (수수료·세금 차감 후)</summary>
    public long RealizedPnl { get; set; }

    public string TimeText => $"{Time.Hours:D2}:{Time.Minutes:D2}";
    public string SideText => Side.ToString();
    public Brush SideBrush => Side == OrderSide.매수 ? Ui.UpBrush : Ui.DownBrush;
    public string Summary => $"{Name} {Qty:N0}주 @ {Price:N0}";
}

public class SaveData
{
    public long BestEquity { get; set; }
    public double BestReturnRate { get; set; }
    public int TotalDaysPlayed { get; set; }
    public bool TutorialCompleted { get; set; }
}
