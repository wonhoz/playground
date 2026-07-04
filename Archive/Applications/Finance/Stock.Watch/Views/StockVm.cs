using System.ComponentModel;
using System.Windows.Media;
using Stock.Watch.Models;

namespace Stock.Watch.Views;

/// <summary>관심종목 리스트 표시용 뷰모델. 시세 갱신 시 PropertyChanged로 UI를 갱신한다.</summary>
public sealed class StockVm : INotifyPropertyChanged
{
    public WatchedStock Stock { get; }

    public StockVm(WatchedStock stock) => Stock = stock;

    public string Display => Stock.Display;
    public string Code => Stock.Code;

    public string PriceText => Stock.LastPrice > 0 ? $"{Stock.LastPrice:N0}" : "-";

    public string ChangeText => Stock.LastPrice > 0
        ? $"{(Stock.LastChangeRate >= 0 ? "+" : "")}{Stock.LastChangeRate:0.##}%"
        : "";

    public Brush ChangeBrush => Stock.LastChangeRate switch
    {
        > 0 => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
        < 0 => new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF)),
        _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
    };

    public void Refresh()
    {
        OnChanged(nameof(Display));
        OnChanged(nameof(PriceText));
        OnChanged(nameof(ChangeText));
        OnChanged(nameof(ChangeBrush));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
