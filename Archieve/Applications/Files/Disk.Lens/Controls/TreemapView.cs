namespace DiskLens.Controls;

/// <summary>Squarified 트리맵 렌더링 커스텀 컨트롤</summary>
public sealed class TreemapView : FrameworkElement
{
    // ── 의존성 프로퍼티 ───────────────────────────────────────────────────────
    public static readonly DependencyProperty RootNodeProperty =
        DependencyProperty.Register(nameof(RootNode), typeof(FileNode), typeof(TreemapView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public FileNode? RootNode
    {
        get => (FileNode?)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    // ── 이벤트 ───────────────────────────────────────────────────────────────
    public event Action<FileNode>? NodeClicked;
    public event Action<FileNode>? NodeRightClicked;

    // ── 내부 상태 ─────────────────────────────────────────────────────────────
    private List<TreemapBlock> _blocks = [];
    private TreemapBlock? _hovered;
    private TreemapBlock? _highlighted;

    private static readonly Typeface _typeface = new("Segoe UI");
    private static readonly Typeface _typefaceBold = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private static readonly Pen _borderPen     = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 0.5));
    private static readonly Pen _hoverPen      = Freeze(new Pen(new SolidColorBrush(Colors.White), 2.0));
    private static readonly Pen _highlightPen  = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)), 2.5));
    private static readonly Pen _folderBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0));

    private static T Freeze<T>(T obj) where T : Freezable { obj.Freeze(); return obj; }

    public TreemapView()
    {
        ClipToBounds = true;
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
            : TreemapLayout.Build(RootNode, new Rect(0, 0, ActualWidth, ActualHeight));
        InvalidateVisual();
    }

    // ── 렌더링 ───────────────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)), null, new Rect(RenderSize));

        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var block in _blocks)
        {
            if (block.Bounds.Width < 1 || block.Bounds.Height < 1) continue;

            bool isHovered     = block == _hovered;
            bool isHighlighted = block == _highlighted;
            bool isDir         = block.Node.IsDirectory;
            bool hasHdr        = isDir && block.Node.Children.Count > 0 && block.Bounds.Height > 20;

            // ── 셀 배경 ──
            var fillColor = block.FillColor;
            if (isHovered)
                fillColor = Brighten(fillColor, 30);

            dc.DrawRectangle(new SolidColorBrush(fillColor), null, block.Bounds);

            // ── 폴더 헤더 바 ──
            if (hasHdr)
            {
                var hdrRect  = new Rect(block.Bounds.X, block.Bounds.Y, block.Bounds.Width, 16);
                var hdrColor = Brighten(fillColor, 20);
                dc.DrawRectangle(new SolidColorBrush(hdrColor), null, hdrRect);

                // 헤더 하단 선
                dc.DrawLine(_folderBorderPen,
                    new Point(block.Bounds.X, block.Bounds.Y + 16),
                    new Point(block.Bounds.Right, block.Bounds.Y + 16));

                // 폴더 레이블 (헤더 안)
                if (block.Bounds.Width > 24)
                    DrawFolderLabel(dc, block, ppd);
            }

            // ── 파일 레이블 (헤더 없는 큰 셀) ──
            if (!hasHdr && block.Bounds.Width > 50 && block.Bounds.Height > 20)
                DrawFileLabel(dc, block, ppd);

            // ── 테두리 ──
            var pen = isHighlighted ? _highlightPen
                    : isHovered     ? _hoverPen
                    : _borderPen;
            dc.DrawRectangle(null, pen, block.Bounds);
        }
    }

    private static Color Brighten(Color c, int amount) => Color.FromRgb(
        (byte)Math.Min(255, c.R + amount),
        (byte)Math.Min(255, c.G + amount),
        (byte)Math.Min(255, c.B + amount));

    private static void DrawFolderLabel(DrawingContext dc, TreemapBlock block, double ppd)
    {
        double maxW = block.Bounds.Width - 20;
        var ft = MakeText(block.Node.Name, _typefaceBold, 10, Colors.White, ppd, maxW);
        dc.DrawText(ft, new Point(block.Bounds.X + 4, block.Bounds.Y + 2));

        // 크기 텍스트 (이름 오른쪽 또는 아래)
        if (block.Bounds.Width > 80)
        {
            var st = MakeText(block.Node.SizeText, _typeface, 9,
                Color.FromArgb(180, 220, 220, 220), ppd, 60);
            double sx = block.Bounds.Right - st.Width - 4;
            if (sx > block.Bounds.X + ft.Width + 6)
                dc.DrawText(st, new Point(sx, block.Bounds.Y + 3));
        }
    }

    private static void DrawFileLabel(DrawingContext dc, TreemapBlock block, double ppd)
    {
        double maxW = block.Bounds.Width - 6;
        var ft = MakeText(block.Node.Name, _typeface, 10, Colors.White, ppd, maxW);
        dc.DrawText(ft, new Point(block.Bounds.X + 3, block.Bounds.Y + 3));

        if (block.Bounds.Height > 30)
        {
            var st = MakeText(block.Node.SizeText, _typeface, 9,
                Color.FromArgb(170, 220, 220, 220), ppd, maxW);
            dc.DrawText(st, new Point(block.Bounds.X + 3, block.Bounds.Y + 15));
        }
    }

    private static FormattedText MakeText(string text, Typeface face, double size, Color color, double ppd, double maxWidth)
    {
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, face, size,
            new SolidColorBrush(color), ppd)
        {
            MaxTextWidth = Math.Max(1, maxWidth),
            MaxLineCount = 1,
            Trimming     = TextTrimming.CharacterEllipsis,
        };
        return ft;
    }

    // ── 마우스 이벤트 ─────────────────────────────────────────────────────────
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var block = HitTest(e.GetPosition(this));
        if (block == _hovered) return;
        _hovered = block;
        InvalidateVisual();
        ToolTip = block is null ? null
            : $"{block.Node.Name}  ({block.Node.SizeText})\n{block.Node.FullPath}";
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
    public void Highlight(string? fullPath)
    {
        _highlighted = fullPath is null ? null
            : _blocks.FirstOrDefault(b => b.Node.FullPath == fullPath);
        InvalidateVisual();
    }

    // 가장 작은(깊은) 블록 우선 — 역순 탐색
    private TreemapBlock? HitTest(Point p)
    {
        for (int i = _blocks.Count - 1; i >= 0; i--)
            if (_blocks[i].Bounds.Contains(p)) return _blocks[i];
        return null;
    }
}
