using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SVG.Forge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;

    // ── Canvas management ────────────────────────────────────────────
    readonly Dictionary<string, FrameworkElement> _shapeMap = [];
    System.Windows.Shapes.Rectangle? _selRect;    // selection overlay rect

    // ── Drawing state ────────────────────────────────────────────────
    bool _isDrawing;
    Point _drawStart;
    SvgElement? _previewEl;
    FrameworkElement? _previewShape;

    // ── Drag state ───────────────────────────────────────────────────
    bool _isDragging;
    SvgElement? _dragEl;
    Point _dragStart;
    double _dragOrigX, _dragOrigY;

    // ── Color picker popup ───────────────────────────────────────────
    System.Windows.Controls.Primitives.Popup? _colorPopup;
    bool _colorTargetFill;

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();

        _vm.CanvasRefreshRequested += RefreshCanvas;
        _vm.ElementChanged         += el => { UpdateShape(el); UpdateSelection(); _vm.RefreshSelectionProperties(); };
        _vm.ExportPngRequested     += DoExportPng;

        Loaded += OnLoaded;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
        DrawGrid();
        RefreshCanvas();
    }

    // ── Zoom / scroll ────────────────────────────────────────────────
    void OnCanvasWheel(object s, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        _vm.ZoomLevel = e.Delta > 0
            ? Math.Min(10, _vm.ZoomLevel * 1.12)
            : Math.Max(0.05, _vm.ZoomLevel / 1.12);
        SvgCanvas.LayoutTransform = new ScaleTransform(_vm.ZoomLevel, _vm.ZoomLevel);
    }

    // ── Grid drawing ─────────────────────────────────────────────────
    void DrawGrid()
    {
        GridCanvas.Children.Clear();
        double w = _vm.Document.CanvasWidth, h = _vm.Document.CanvasHeight;
        const double step = 20;
        var pen = new System.Windows.Media.Pen(new SolidColorBrush(Color.FromArgb(30, 180, 180, 255)), 0.5);

        for (double x = 0; x <= w; x += step)
            GridCanvas.Children.Add(new Line
                { X1 = x, Y1 = 0, X2 = x, Y2 = h, Stroke = pen.Brush, StrokeThickness = pen.Thickness });
        for (double y = 0; y <= h; y += step)
            GridCanvas.Children.Add(new Line
                { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = pen.Brush, StrokeThickness = pen.Thickness });
    }

    // ── Canvas refresh ───────────────────────────────────────────────
    void RefreshCanvas()
    {
        // Update background
        BgRect.Fill = new SolidColorBrush(_vm.Document.Background);

        // Rebuild shape map
        ShapesCanvas.Children.Clear();
        _shapeMap.Clear();
        foreach (var el in _vm.Document.AllElements)
            AddShapeToCanvas(el);
        DrawGrid();
        UpdateSelection();
    }

    void AddShapeToCanvas(SvgElement el)
    {
        var fe = CreateWpfShape(el);
        _shapeMap[el.Id] = fe;
        ShapesCanvas.Children.Add(fe);
        // Subscribe to property changes
        el.PropertyChanged += (_, __) => UpdateShape(el);
    }

    void UpdateShape(SvgElement el)
    {
        if (!_shapeMap.TryGetValue(el.Id, out var fe)) return;
        ApplyStyle(el, fe);
        PositionElement(el, fe);
    }

    FrameworkElement CreateWpfShape(SvgElement el)
    {
        FrameworkElement fe;
        if (el.ShapeType == SvgShapeType.Text)
        {
            fe = new TextBlock
            {
                FontFamily = new FontFamily(el.FontFamily),
                FontSize   = el.FontSize,
            };
        }
        else if (el.ShapeType == SvgShapeType.Line)
        {
            fe = new Line();
        }
        else if (el.ShapeType == SvgShapeType.Ellipse)
        {
            fe = new Ellipse();
        }
        else
        {
            fe = new System.Windows.Shapes.Rectangle();
        }

        ApplyStyle(el, fe);
        PositionElement(el, fe);
        fe.Tag = el.Id;
        fe.Cursor = Cursors.Arrow;
        fe.MouseLeftButtonDown += ShapeMouseDown;
        return fe;
    }

    void ApplyStyle(SvgElement el, FrameworkElement fe)
    {
        var fill   = el.HasFill   ? new SolidColorBrush(el.FillColor)   : (Brush)Brushes.Transparent;
        var stroke = el.HasStroke ? new SolidColorBrush(el.StrokeColor) : (Brush)Brushes.Transparent;

        switch (fe)
        {
            case System.Windows.Shapes.Rectangle r:
                r.Width           = Math.Max(1, el.W);
                r.Height          = Math.Max(1, el.H);
                r.Fill            = fill;
                r.Stroke          = stroke;
                r.StrokeThickness = el.StrokeWidth;
                r.Opacity         = el.Opacity;
                break;
            case Ellipse ell:
                ell.Width           = Math.Max(1, el.W);
                ell.Height          = Math.Max(1, el.H);
                ell.Fill            = fill;
                ell.Stroke          = stroke;
                ell.StrokeThickness = el.StrokeWidth;
                ell.Opacity         = el.Opacity;
                break;
            case Line ln:
                ln.X1              = el.X;
                ln.Y1              = el.Y;
                ln.X2              = el.X2;
                ln.Y2              = el.Y2;
                ln.Stroke          = stroke;
                ln.StrokeThickness = Math.Max(1, el.StrokeWidth);
                ln.Opacity         = el.Opacity;
                break;
            case TextBlock tb:
                tb.Text           = el.Text;
                tb.FontFamily     = new FontFamily(el.FontFamily);
                tb.FontSize       = Math.Max(4, el.FontSize);
                tb.Foreground     = el.HasFill ? new SolidColorBrush(el.FillColor) : Brushes.Black;
                tb.Opacity        = el.Opacity;
                break;
        }
    }

    void PositionElement(SvgElement el, FrameworkElement fe)
    {
        if (fe is Line) return; // Line uses X1/Y1/X2/Y2 directly
        Canvas.SetLeft(fe, el.X);
        Canvas.SetTop(fe,  el.Y);
    }

    // ── Selection overlay ─────────────────────────────────────────────
    void UpdateSelection()
    {
        SelectionCanvas.Children.Clear();
        _selRect = null;
        var el = _vm.SelectedElement;
        if (el == null) return;

        var (bx, by, bw, bh) = GetBounds(el);
        const double pad = 3;

        _selRect = new System.Windows.Shapes.Rectangle
        {
            Width           = bw + pad * 2,
            Height          = bh + pad * 2,
            Stroke          = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill            = Brushes.Transparent,
        };
        Canvas.SetLeft(_selRect, bx - pad);
        Canvas.SetTop(_selRect,  by - pad);
        SelectionCanvas.Children.Add(_selRect);

        // Corner handles
        foreach (var (hx, hy) in new[]
        {
            (bx,        by),        (bx + bw/2, by),        (bx + bw, by),
            (bx,        by + bh/2),                          (bx + bw, by + bh/2),
            (bx,        by + bh),   (bx + bw/2, by + bh),   (bx + bw, by + bh),
        })
        {
            var h = new System.Windows.Shapes.Rectangle
            {
                Width  = 7, Height = 7,
                Fill   = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(h, hx - 3.5);
            Canvas.SetTop(h,  hy - 3.5);
            SelectionCanvas.Children.Add(h);
        }
    }

    static (double x, double y, double w, double h) GetBounds(SvgElement el)
    {
        if (el.ShapeType == SvgShapeType.Line)
        {
            double minX = Math.Min(el.X, el.X2), minY = Math.Min(el.Y, el.Y2);
            double maxX = Math.Max(el.X, el.X2), maxY = Math.Max(el.Y, el.Y2);
            return (minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
        }
        return (el.X, el.Y, el.W, el.H);
    }

    // ── Tool button toggle ────────────────────────────────────────────
    void ToolBtn_Click(object s, RoutedEventArgs e)
    {
        if (s is not System.Windows.Controls.Primitives.ToggleButton tb) return;
        var tool = tb.Tag?.ToString() ?? "Select";
        _vm.SetToolCmd.Execute(tool);

        // Uncheck others
        foreach (var btn in new[] { BtnToolSelect, BtnToolRect, BtnToolEllipse, BtnToolLine, BtnToolText })
            btn.IsChecked = btn == tb;

        _vm.Status = $"도구: {tool}";
    }

    // ── Canvas mouse events ──────────────────────────────────────────
    void OnCanvasMouseDown(object s, MouseButtonEventArgs e)
    {
        SvgCanvas.Focus();
        var p = e.GetPosition(SvgCanvas);

        if (_vm.CurrentTool == ToolMode.Select) return; // handled by ShapeMouseDown

        // Start drawing
        _isDrawing = true;
        _drawStart = p;
        SvgCanvas.CaptureMouse();
        e.Handled = true;

        _previewEl = new SvgElement
        {
            ShapeType   = _vm.CurrentTool switch
            {
                ToolMode.Rect    => SvgShapeType.Rect,
                ToolMode.Ellipse => SvgShapeType.Ellipse,
                ToolMode.Line    => SvgShapeType.Line,
                ToolMode.Text    => SvgShapeType.Text,
                _                => SvgShapeType.Rect,
            },
            X = p.X, Y = p.Y, W = 1, H = 1,
            X2 = p.X, Y2 = p.Y,
        };

        if (_vm.CurrentTool == ToolMode.Text)
        {
            // Place text immediately
            _isDrawing = false;
            SvgCanvas.ReleaseMouseCapture();
            var el = _vm.AddElement(_previewEl);
            el.W = 120; el.H = 30;
            AddShapeToCanvas(el);
            UpdateSelection();
            _previewEl = null;
            return;
        }

        _previewShape = CreatePreviewShape(_previewEl);
        PreviewCanvas.Children.Add(_previewShape!);
    }

    void OnCanvasMouseMove(object s, MouseEventArgs e)
    {
        var p = e.GetPosition(SvgCanvas);

        if (_isDragging && _dragEl != null)
        {
            var dx = p.X - _dragStart.X;
            var dy = p.Y - _dragStart.Y;

            if (_dragEl.ShapeType == SvgShapeType.Line)
            {
                _dragEl.X  = _dragOrigX + dx;
                _dragEl.Y  = _dragOrigY + dy;
                _dragEl.X2 = _dragEl.X2 + (dx - (p.X - _dragStart.X - dx));
                // Simpler: move both endpoints
                // Reset and recalc
            }
            else
            {
                _dragEl.X = _dragOrigX + dx;
                _dragEl.Y = _dragOrigY + dy;
            }

            if (_shapeMap.TryGetValue(_dragEl.Id, out var fe))
                PositionElement(_dragEl, fe);
            UpdateSelection();
            _vm.RefreshSelectionProperties();
            return;
        }

        if (!_isDrawing || _previewEl == null) return;

        var rect = MakeRect(_drawStart, p);
        _previewEl.X = rect.X;  _previewEl.Y = rect.Y;
        _previewEl.W = rect.Width; _previewEl.H = rect.Height;

        if (_previewEl.ShapeType == SvgShapeType.Line)
        {
            _previewEl.X = _drawStart.X; _previewEl.Y = _drawStart.Y;
            _previewEl.X2 = p.X; _previewEl.Y2 = p.Y;
        }

        if (_previewShape != null)
            ApplyStyle(_previewEl, _previewShape);
        if (_previewShape != null)
            PositionPreview(_previewEl, _previewShape);
    }

    void OnCanvasMouseUp(object s, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _dragEl = null;
            _vm.Document.IsDirty = true;
            return;
        }

        if (!_isDrawing || _previewEl == null) return;
        _isDrawing = false;
        SvgCanvas.ReleaseMouseCapture();

        var p = e.GetPosition(SvgCanvas);
        var minSize = _previewEl.ShapeType == SvgShapeType.Line ? 5.0 : 10.0;

        bool valid = _previewEl.ShapeType == SvgShapeType.Line
            ? Math.Abs(p.X - _drawStart.X) > minSize || Math.Abs(p.Y - _drawStart.Y) > minSize
            : _previewEl.W > minSize && _previewEl.H > minSize;

        if (_previewShape != null) PreviewCanvas.Children.Remove(_previewShape);
        _previewShape = null;

        if (valid)
        {
            var el = _vm.AddElement(_previewEl);
            AddShapeToCanvas(el);
            UpdateSelection();
        }

        _previewEl = null;
    }

    void OnCanvasMouseLeave(object s, MouseEventArgs e)
    {
        if (_isDragging || _isDrawing)
            OnCanvasMouseUp(s, null!);
    }

    void ShapeMouseDown(object s, MouseButtonEventArgs e)
    {
        if (_vm.CurrentTool != ToolMode.Select) return;

        var id = (s as FrameworkElement)?.Tag?.ToString();
        if (id == null) return;

        var el = _vm.Document.AllElements.FirstOrDefault(x => x.Id == id);
        if (el == null || el.IsLocked) return;

        _vm.SelectedElement = el;
        UpdateSelection();
        UpdateHexBoxes();

        _isDragging = true;
        _dragEl     = el;
        _dragStart  = e.GetPosition(SvgCanvas);
        _dragOrigX  = el.X;
        _dragOrigY  = el.Y;
        e.Handled   = true;
    }

    // ── Layer/element panel clicks ───────────────────────────────────
    void LayerHeader_Click(object s, MouseButtonEventArgs e)
    {
        if (s is not Border b) return;
        var layer = b.DataContext as SvgLayer;
        if (layer != null) _vm.SelectedLayer = layer;
    }

    void ElementItem_Click(object s, MouseButtonEventArgs e)
    {
        if (s is not Border b) return;
        var el = b.DataContext as SvgElement;
        if (el == null) return;
        _vm.SelectedElement = el;
        UpdateSelection();
        UpdateHexBoxes();
        HighlightLayerElement(el);
    }

    void HighlightLayerElement(SvgElement el)
    {
        // Update layer item backgrounds
        foreach (var layer in _vm.Layers)
        {
            if (layer.Elements.Contains(el))
                _vm.SelectedLayer = layer;
        }
    }

    // ── Property panel ────────────────────────────────────────────────
    void PropTextBox_LostFocus(object s, RoutedEventArgs e)
    {
        if (!IsLoaded || _vm.SelectedElement == null) return;
        // Binding handles the update; force canvas refresh
        UpdateShape(_vm.SelectedElement);
        UpdateSelection();
    }

    void HexBox_LostFocus(object s, RoutedEventArgs e)
    {
        if (!IsLoaded || _vm.SelectedElement == null) return;
        if (s is not TextBox tb) return;
        var hex = tb.Text.Trim().TrimStart('#');
        if (hex.Length == 6 && TryParseHex(hex, out var c))
        {
            if (tb.Tag?.ToString() == "fill")
            {
                _vm.SelFillColor = c;
                ((SolidColorBrush)FillColorSwatch.Fill).Color = c;
            }
            else
            {
                _vm.SelStrokeColor = c;
                ((SolidColorBrush)StrokeColorSwatch.Fill).Color = c;
            }
            UpdateShape(_vm.SelectedElement);
        }
    }

    void StrokeSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _vm.SelectedElement == null) return;
        UpdateShape(_vm.SelectedElement);
    }

    void OpacitySlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _vm.SelectedElement == null) return;
        UpdateShape(_vm.SelectedElement);
    }

    void FontSizeSlider_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _vm.SelectedElement == null) return;
        UpdateShape(_vm.SelectedElement);
    }

    void UpdateHexBoxes()
    {
        var el = _vm.SelectedElement;
        if (el == null) return;
        TbFillHex.Text   = $"#{el.FillColor.R:X2}{el.FillColor.G:X2}{el.FillColor.B:X2}";
        TbStrokeHex.Text = $"#{el.StrokeColor.R:X2}{el.StrokeColor.G:X2}{el.StrokeColor.B:X2}";
    }

    // ── Color swatch click — simple color picker popup ────────────────
    void FillSwatch_Click(object s, MouseButtonEventArgs e) => ShowColorPicker(true);
    void StrokeSwatch_Click(object s, MouseButtonEventArgs e) => ShowColorPicker(false);

    void ShowColorPicker(bool forFill)
    {
        if (_vm.SelectedElement == null) return;
        _colorTargetFill = forFill;

        var colors = new[]
        {
            "#FF0000","#FF6600","#FFCC00","#FFFF00","#99FF00","#33FF00",
            "#00FF66","#00FFCC","#00CCFF","#0066FF","#3300FF","#9900FF",
            "#FF00CC","#FF0066","#FF9999","#FFCC99","#FFFF99","#CCFF99",
            "#99FFCC","#99CCFF","#9999FF","#CC99FF","#FF99CC","#FFFFFF",
            "#CCCCCC","#999999","#666666","#333333","#000000","#1A1A2A",
            "#2563EB","#10B981","#F59E0B","#EF4444","#8B5CF6","#EC4899",
            "#14B8A6","#F97316","#4682B4","#8B4513",
        };

        var wrap = new WrapPanel { Width = 168 };
        foreach (var hex in colors)
        {
            var hexLocal = hex;
            var r = new System.Windows.Shapes.Rectangle
            {
                Width = 20, Height = 20, Margin = new Thickness(1),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexLocal)),
                Cursor = Cursors.Hand,
            };
            r.MouseLeftButtonDown += (_, __) => ApplyPickedColor(hexLocal);
            r.ToolTip = hexLocal;
            wrap.Children.Add(r);
        }

        var hexInput = new TextBox { Width = 80, Margin = new Thickness(4, 6, 4, 4), FontSize = 10 };
        var applyBtn = new Button { Content = "적용", Margin = new Thickness(4, 6, 4, 4), FontSize = 10, Padding = new Thickness(8, 2, 8, 2) };
        applyBtn.Click += (_, __) => ApplyPickedColor(hexInput.Text.Trim());

        var panel = new StackPanel { Background = new SolidColorBrush(Color.FromRgb(22, 22, 40)) };
        panel.Children.Add(wrap);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(hexInput);
        row.Children.Add(applyBtn);
        panel.Children.Add(row);

        var popup = new System.Windows.Controls.Primitives.Popup
        {
            Child       = new Border { Child = panel, BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 62)), BorderThickness = new Thickness(1) },
            PlacementTarget = forFill ? FillColorSwatch : StrokeColorSwatch,
            Placement   = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen   = false,
            IsOpen      = true,
            AllowsTransparency = true,
        };
        _colorPopup = popup;
    }

    void ApplyPickedColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 || !TryParseHex(hex, out var c)) return;
        _colorPopup?.SetCurrentValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);

        if (_colorTargetFill)
        {
            _vm.SelFillColor = c;
            ((SolidColorBrush)FillColorSwatch.Fill).Color = c;
            TbFillHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        else
        {
            _vm.SelStrokeColor = c;
            ((SolidColorBrush)StrokeColorSwatch.Fill).Color = c;
            TbStrokeHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        if (_vm.SelectedElement != null) UpdateShape(_vm.SelectedElement);
    }

    // ── PNG export ───────────────────────────────────────────────────
    void DoExportPng(string path)
    {
        try
        {
            ExportService.ExportPng(SvgCanvas, _vm.Document.CanvasWidth, _vm.Document.CanvasHeight, path);
            _vm.Status = $"PNG 내보내기 완료: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PNG 내보내기 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────
    static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));

    FrameworkElement? CreatePreviewShape(SvgElement el)
    {
        FrameworkElement fe = el.ShapeType switch
        {
            SvgShapeType.Ellipse => new Ellipse { Fill = new SolidColorBrush(Color.FromArgb(80, 70, 130, 180)), Stroke = Brushes.CornflowerBlue, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 2 } },
            SvgShapeType.Line    => new Line    { Stroke = Brushes.CornflowerBlue, StrokeThickness = 1.5 },
            _                    => new System.Windows.Shapes.Rectangle { Fill = new SolidColorBrush(Color.FromArgb(80, 70, 130, 180)), Stroke = Brushes.CornflowerBlue, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 2 } },
        };
        return fe;
    }

    void PositionPreview(SvgElement el, FrameworkElement fe)
    {
        switch (fe)
        {
            case System.Windows.Shapes.Rectangle r:
                r.Width = Math.Max(1, el.W); r.Height = Math.Max(1, el.H);
                Canvas.SetLeft(r, el.X); Canvas.SetTop(r, el.Y);
                break;
            case Ellipse ell:
                ell.Width = Math.Max(1, el.W); ell.Height = Math.Max(1, el.H);
                Canvas.SetLeft(ell, el.X); Canvas.SetTop(ell, el.Y);
                break;
            case Line ln:
                ln.X1 = el.X; ln.Y1 = el.Y; ln.X2 = el.X2; ln.Y2 = el.Y2;
                break;
        }
    }

    static bool TryParseHex(string hex, out Color c)
    {
        c = Colors.Black;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return false;
        try
        {
            c = Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
            return true;
        }
        catch { return false; }
    }
}
