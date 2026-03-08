namespace QuickCalc.ViewModels;

/// <summary>비트 토글 버튼 하나를 나타내는 모델</summary>
public class BitItem(int index, MainViewModel owner) : INotifyPropertyChanged
{
    private bool _isOn;

    /// <summary>비트 인덱스 (63 = MSB, 0 = LSB)</summary>
    public int Index { get; } = index;

    public bool IsOn
    {
        get => _isOn;
        set { _isOn = value; OnPropertyChanged(); }
    }

    /// <summary>비트가 속하는 구분: Sign(S) / Exp32(E) / Man32(M) / Exp64(X) / Man64(N) / None</summary>
    public string IeeePart { get; set; } = "None";

    public ICommand ToggleCommand { get; } = new RelayCommand(_ => owner.ToggleBit(index));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
