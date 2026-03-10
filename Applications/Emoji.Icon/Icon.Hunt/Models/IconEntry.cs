using System.ComponentModel;
using System.Windows.Media;

namespace IconHunt.Models;

public class IconEntry : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;          // "mdi:home"
    public string Prefix { get; set; } = string.Empty;      // "mdi"
    public string Name { get; set; } = string.Empty;        // "home"
    public string CollectionName { get; set; } = string.Empty; // "Material Design Icons"
    public string License { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;        // 쉼표 구분

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set { _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); }
    }

    private string? _localSvgPath;
    public string? LocalSvgPath
    {
        get => _localSvgPath;
        set { _localSvgPath = value; OnPropertyChanged(nameof(LocalSvgPath)); }
    }

    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
    }

    // 표시용
    public string DisplayName => Name.Length > 18 ? Name[..18] + "…" : Name;
    public string BadgeText => Prefix.ToUpperInvariant();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
