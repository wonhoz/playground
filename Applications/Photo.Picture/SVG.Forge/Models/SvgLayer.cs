namespace SVG.Forge.Models;

public class SvgLayer : INotifyPropertyChanged
{
    string _name = "레이어";
    bool _isVisible = true;
    bool _isLocked;

    public string Name      { get => _name;      set { _name = value;      Notify(); } }
    public bool   IsVisible { get => _isVisible; set { _isVisible = value; Notify(); } }
    public bool   IsLocked  { get => _isLocked;  set { _isLocked = value;  Notify(); } }

    public ObservableCollection<SvgElement> Elements { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new(p));
}
