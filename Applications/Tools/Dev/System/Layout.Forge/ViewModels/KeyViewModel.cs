namespace Layout.Forge.ViewModels;

using Layout.Forge.Models;

public class KeyViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public KeyDef Key { get; }

    ushort? _mappedTo;
    bool    _isSelected;

    public KeyViewModel(KeyDef key) => Key = key;

    public ushort? MappedTo
    {
        get => _mappedTo;
        set
        {
            _mappedTo = value;
            Notify(nameof(MappedTo));
            Notify(nameof(IsRemapped));
            Notify(nameof(IsDisabled));
            Notify(nameof(BadgeText));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; Notify(nameof(IsSelected)); }
    }

    public bool IsRemapped  => _mappedTo.HasValue;
    public bool IsDisabled  => _mappedTo == 0x0000;

    /// <summary>리매핑 대상 이름을 약식으로 표시 (뱃지)</summary>
    public string BadgeText => IsRemapped
        ? (IsDisabled ? "off" : "→ " + ShortName(_mappedTo!.Value))
        : "";

    static string ShortName(ushort sc)
    {
        var name = KeyboardLayout.GetKeyName(sc);
        return name.Length > 6 ? name[..6] : name;
    }

    void Notify([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
