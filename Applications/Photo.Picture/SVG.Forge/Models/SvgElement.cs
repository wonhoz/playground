namespace SVG.Forge.Models;

public class SvgElement : INotifyPropertyChanged
{
    string _name = "요소";
    bool _isVisible = true;
    bool _isLocked;
    double _x, _y, _w = 120, _h = 80;
    double _x2 = 120, _y2 = 80;
    Color _fillColor = Color.FromRgb(70, 130, 180);
    bool _hasFill = true;
    Color _strokeColor = Color.FromRgb(100, 100, 100);
    bool _hasStroke = true;
    double _strokeWidth = 1.5;
    double _opacity = 1.0;
    double _rotation;
    string _text = "텍스트";
    string _fontFamily = "Segoe UI";
    double _fontSize = 16;

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public SvgShapeType ShapeType { get; set; }

    public string Name         { get => _name;        set { _name = value;        Notify(); } }
    public bool   IsVisible    { get => _isVisible;   set { _isVisible = value;   Notify(); } }
    public bool   IsLocked     { get => _isLocked;    set { _isLocked = value;    Notify(); } }
    public double X            { get => _x;           set { _x = value;           Notify(); } }
    public double Y            { get => _y;           set { _y = value;           Notify(); } }
    public double W            { get => _w;           set { _w = Math.Max(1, value); Notify(); } }
    public double H            { get => _h;           set { _h = Math.Max(1, value); Notify(); } }
    public double X2           { get => _x2;          set { _x2 = value;          Notify(); } }
    public double Y2           { get => _y2;          set { _y2 = value;          Notify(); } }
    public Color  FillColor    { get => _fillColor;   set { _fillColor = value;   Notify(); } }
    public bool   HasFill      { get => _hasFill;     set { _hasFill = value;     Notify(); } }
    public Color  StrokeColor  { get => _strokeColor; set { _strokeColor = value; Notify(); } }
    public bool   HasStroke    { get => _hasStroke;   set { _hasStroke = value;   Notify(); } }
    public double StrokeWidth  { get => _strokeWidth; set { _strokeWidth = Math.Max(0, value); Notify(); } }
    public double Opacity      { get => _opacity;     set { _opacity = Math.Clamp(value, 0, 1); Notify(); } }
    public double Rotation     { get => _rotation;    set { _rotation = value;    Notify(); } }
    public string Text         { get => _text;        set { _text = value;        Notify(); } }
    public string FontFamily   { get => _fontFamily;  set { _fontFamily = value;  Notify(); } }
    public double FontSize     { get => _fontSize;    set { _fontSize = Math.Max(4, value); Notify(); } }

    public SvgElement Clone() => new()
    {
        ShapeType   = ShapeType,
        _name       = Name + " 복사",
        _x          = X + 10,  _y  = Y + 10,
        _w          = W,        _h  = H,
        _x2         = X2 + 10, _y2 = Y2 + 10,
        _fillColor  = FillColor,    _hasFill   = HasFill,
        _strokeColor= StrokeColor,  _hasStroke = HasStroke,
        _strokeWidth= StrokeWidth,  _opacity   = Opacity,
        _rotation   = Rotation,
        _text       = Text,     _fontFamily = FontFamily, _fontSize = FontSize,
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new(p));
}
