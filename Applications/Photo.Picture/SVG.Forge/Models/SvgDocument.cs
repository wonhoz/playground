namespace SVG.Forge.Models;

public class SvgDocument : INotifyPropertyChanged
{
    double _canvasWidth = 800;
    double _canvasHeight = 600;
    Color _background = Colors.White;
    string? _filePath;
    bool _isDirty;

    public double CanvasWidth  { get => _canvasWidth;  set { _canvasWidth = value;  Notify(); } }
    public double CanvasHeight { get => _canvasHeight; set { _canvasHeight = value; Notify(); } }
    public Color  Background   { get => _background;   set { _background = value;   Notify(); } }
    public bool   IsDirty      { get => _isDirty;      set { _isDirty = value; Notify(); Notify(nameof(Title)); } }

    public string? FilePath
    {
        get => _filePath;
        set { _filePath = value; Notify(); Notify(nameof(Title)); }
    }

    public string Title =>
        (IsDirty ? "● " : "") +
        (FilePath != null ? Path.GetFileName(FilePath) : "새 문서");

    public ObservableCollection<SvgLayer> Layers { get; } = [];

    public IEnumerable<SvgElement> AllElements =>
        Layers.Where(l => l.IsVisible).SelectMany(l => l.Elements);

    public static SvgDocument CreateDefault()
    {
        var doc = new SvgDocument();
        doc.Layers.Add(new SvgLayer { Name = "레이어 1" });
        return doc;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new(p));
}
