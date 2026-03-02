using System.Windows.Media.Effects;

namespace DiskLens.Controls;

/// <summary>Squarified 트리맵 렌더링 커스텀 컨트롤</summary>
public sealed class TreemapView : FrameworkElement
{
    // ── 의존성 프로퍼티 ───────────────────────────────────────────────────────
    public static readonly DependencyProperty RootNodeProperty =
        DependencyProperty.Register(nameof(RootNode), typeof(FileNode), typeof(TreemapView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public static readonly DependencyProperty MaxDepthProperty =
        DependencyProperty.Register(nameof(MaxDepth), typeof(int), typeof(TreemapView),
            new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public FileNode? RootNode
    {
        get => (FileNode?)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public int MaxDepth
    {
        get => (int)GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────────
    public event Action<FileNode>? NodeClicked;   // 드릴다운
    public event Action<FileNode>? NodeRightClicked;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────
    private List<TreemapBlock> _blocks = [];
    private TreemapBlock? _hovered;
    private TreemapBlock? _highlighted; // TOP20 하이라이트용

    private static readonly Typeface _typeface = new("Segoe UI");
    private static readonly Pen _borderPen     = new(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
    private static readonly Pen _hoverPen      = new(new SolidColorBrush(Colors.White), 1.5);
    private static readonly Pen _highlightPen  = new(new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)), 2.0);

    static TreemapView()
    {
        _borderPen.Freeze();
        _hoverPen.Freeze();
        _highlightPen.Freeze();
    }

    public TreemapView()
    {
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    // ── 레이아웃 ─────────────────────────────────────────────────────────────
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        Rebuild();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapView tv) tv.Rebuild();
    }

    private void Rebuild()
    {
        _blocks = RootNode is null
            ? []
            : TreemapLayout.Build(RootNode, new Rect(0, 0, ActualWidth, ActualHeight), MaxDepth);
        InvalidateVisual();
    }

    // ── 렌더링 ───────────────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), null, new Rect(RenderSize));

        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var block in _blocks)
        {
            if (block.Bounds.Width < 1 || block.Bounds.Height < 1) continue;

            var fill = new SolidColorBrush(block.FillColor);
            if (block == _hovered)
            {
                var hc = block.FillColor;
                fill = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, hc.R + 40),
                    (byte)Math.Min(255, hc.G + 40),
                    (byte)Math.Min(255, hc.B + 40)));
            }

            dc.DrawRectangle(fill, null, block.Bounds);

            var pen = block == _highlighted ? _highlightPen
                    : block == _hovered     ? _hoverPen
                    : _borderPen;
            dc.DrawRectangle(null, pen, block.Bounds);

            if (block.Bounds.Width > 50 && block.Bounds.Height > 18)
                DrawLabel(dc, block, ppd);
        }
    }

    private static void DrawLabel(DrawingContext dc, TreemapBlock block, double ppd)
    {
        double fontSize = block.Bounds.Height > 40 ? 11 : 9;
        string label    = block.Node.Name;
        if (block.Bounds.Width < 80  && label.Length > 10) label = label[..8]  + "…";
        else if (block.Bounds.Width < 140 && label.Length > 20) label = label[..18] + "…";

        var ft = new FormattedText(label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, _typeface, fontSize, Brushes.White, ppd)
        {
            MaxTextWidth = block.Bounds.Width - 4,
            MaxLineCount = 1,
            Trimming     = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(ft, new Point(block.Bounds.X + 3, block.Bounds.Y + 3));

        if (block.Bounds.Height > 32)
        {
            var st = new FormattedText(block.Node.SizeText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, _typeface, 9,
                new SolidColorBrush(Color.FromArgb(180, 220, 220, 220)), ppd)
            {
                MaxTextWidth = block.Bounds.Width - 4,
                MaxLineCount = 1,
                Trimming     = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(st, new Point(block.Bounds.X + 3, block.Bounds.Y + 3 + fontSize + 1));
        }
    }

    // ── 마우스 이벤트 ─────────────────────────────────────────────────────────
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos   = e.GetPosition(this);
        var block = HitTest(pos);
        if (block == _hovered) return;
        _hovered = block;
        InvalidateVisual();

        ToolTip = block is null ? null
            : $"{block.Node.Name}\n{block.Node.SizeText}\n{block.Node.FullPath}";
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var block = HitTest(e.GetPosition(this));
        if (block?.Node.IsDirectory == true)
            NodeClicked?.Invoke(block.Node);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        var block = HitTest(e.GetPosition(this));
        if (block != null)
            NodeRightClicked?.Invoke(block.Node);
    }

    // ── 공개 메서드 ───────────────────────────────────────────────────────────
    /// <summary>특정 파일 하이라이트 (TOP20 목록 선택 시)</summary>
    public void Highlight(string? fullPath)
    {
        _highlighted = fullPath is null ? null
            : _blocks.FirstOrDefault(b => b.Node.FullPath == fullPath);
        InvalidateVisual();
        if (_highlighted != null)
            ScrollIntoView(_highlighted.Bounds);
    }

    private void ScrollIntoView(Rect bounds)
    {
        // TreemapView는 자체 스크롤 없음. 시각적 표시만으로 충분.
    }

    private TreemapBlock? HitTest(Point p)
    {
        // 가장 작은(깊은) 블록 우선 — 역순 탐색
        for (int i = _blocks.Count - 1; i >= 0; i--)
        {
            if (_blocks[i].Bounds.Contains(p)) return _blocks[i];
        }
        return null;
    }
}
