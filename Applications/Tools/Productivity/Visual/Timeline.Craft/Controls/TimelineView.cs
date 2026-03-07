using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Timeline.Craft.Controls;

/// <summary>타임라인 캔버스 컨트롤</summary>
sealed class TimelineView : Control
{
    // ── 상수 ─────────────────────────────────────────────────────────────────
    const double AxisH    = 48;  // 시간축 헤더 높이
    const double LaneH    = 56;  // 레인 행 높이
    const double LanePad  = 6;   // 레인 내 이벤트 수직 패딩
    const double LabelW   = 110; // 레인 레이블 폭

    // ── 색상 ─────────────────────────────────────────────────────────────────
    static readonly Color BgColor    = Color.FromRgb(0x13, 0x13, 0x1F);
    static readonly Color AxisBg     = Color.FromRgb(0x0A, 0x0A, 0x14);
    static readonly Color LaneBg     = Color.FromRgb(0x16, 0x16, 0x24);
    static readonly Color LaneAlt    = Color.FromRgb(0x12, 0x12, 0x1E);
    static readonly Color LabelBg    = Color.FromRgb(0x0F, 0x0F, 0x1A);
    static readonly Color GridLine   = Color.FromRgb(0x22, 0x22, 0x35);
    static readonly Color AxisText   = Color.FromRgb(0x77, 0x77, 0x99);
    static readonly Color TodayLine  = Color.FromRgb(0xF5, 0x9E, 0x0B);
    static readonly Color SelectBdr  = Color.FromRgb(0xFF, 0xFF, 0xFF);

    // ── 의존성 프로퍼티 ──────────────────────────────────────────────────────
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel),
            typeof(TimelineView), new PropertyMetadata(null, OnVmChanged));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    static void OnVmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tv = (TimelineView)d;
        if (e.OldValue is MainViewModel old) old.RebuildRequested -= tv.RebuildAll;
        if (e.NewValue is MainViewModel nv)  nv.RebuildRequested  += tv.RebuildAll;
        tv.RebuildAll();
    }

    // ── 내부 필드 ─────────────────────────────────────────────────────────────
    Canvas _root   = null!;
    Canvas _axisC  = null!;    // 시간축 (고정 상단)
    Canvas _lanesC = null!;    // 레인 배경
    Canvas _eventsC = null!;   // 이벤트 블록

    // 드래그 상태
    TimelineEventBlock? _dragging;
    Point               _dragStart;
    DateTime            _dragOrigStart;
    bool                _dragResizing;
    bool                _draggingBlock;

    // 그리기 상태
    bool _isCreating;
    Point _createStart;
    Rectangle? _createRect;

    public TimelineView()
    {
        Focusable = true;
        ClipToBounds = true;
        Background = new SolidColorBrush(BgColor);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        RebuildAll();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        _root    = new Canvas { Background = new SolidColorBrush(BgColor) };
        _lanesC  = new Canvas();
        _axisC   = new Canvas { Height = AxisH };
        _eventsC = new Canvas();

        _root.Children.Add(_lanesC);
        _root.Children.Add(_eventsC);
        _root.Children.Add(_axisC);

        AddVisualChild(_root);
        AddLogicalChild(_root);

        _eventsC.MouseLeftButtonDown += OnEventsMouseDown;
        _eventsC.MouseMove           += OnEventsMouseMove;
        _eventsC.MouseLeftButtonUp   += OnEventsMouseUp;
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _root;

    protected override Size MeasureOverride(Size constraint)
    {
        var sz = CalcSize();
        _root.Measure(sz);
        return sz;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var sz = CalcSize();
        _root.Arrange(new Rect(0, 0, sz.Width, sz.Height));
        return sz;
    }

    Size CalcSize()
    {
        var vm = ViewModel;
        if (vm == null) return new Size(800, 400);
        int laneCount = Math.Max(1, vm.Lanes.Count);
        double totalDays = vm.VisibleDays + 14;
        return new Size(LabelW + totalDays * vm.PixelsPerDay,
                        AxisH + laneCount * LaneH);
    }

    // ── 전체 재구성 ────────────────────────────────────────────────────────────

    public void RebuildAll()
    {
        if (_root == null) return;
        _lanesC.Children.Clear();
        _axisC.Children.Clear();
        _eventsC.Children.Clear();

        var vm = ViewModel;
        if (vm == null) return;

        DrawLanes(vm);
        DrawAxis(vm);
        DrawEvents(vm);

        var sz = CalcSize();
        _root.Width  = sz.Width;
        _root.Height = sz.Height;
        Width  = sz.Width;
        Height = sz.Height;

        Canvas.SetLeft(_axisC,   0);
        Canvas.SetTop(_axisC,    0);
        Canvas.SetLeft(_lanesC,  0);
        Canvas.SetTop(_lanesC,   AxisH);
        Canvas.SetLeft(_eventsC, 0);
        Canvas.SetTop(_eventsC,  AxisH);
    }

    // ── 레인 배경 ─────────────────────────────────────────────────────────────

    void DrawLanes(MainViewModel vm)
    {
        double w = LabelW + vm.VisibleDays * vm.PixelsPerDay + 200;

        for (int i = 0; i < vm.Lanes.Count; i++)
        {
            var lane = vm.Lanes[i];
            bool alt = i % 2 == 1;

            var bg = new Rectangle
            {
                Width  = w,
                Height = LaneH,
                Fill   = new SolidColorBrush(alt ? LaneAlt : LaneBg)
            };
            Canvas.SetLeft(bg, 0);
            Canvas.SetTop(bg, i * LaneH);
            _lanesC.Children.Add(bg);

            // 레인 레이블
            var lbl = new Border
            {
                Width      = LabelW,
                Height     = LaneH,
                Background = new SolidColorBrush(LabelBg),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22,0x22,0x35)),
                BorderThickness = new Thickness(0,0,1,1),
                Child      = new TextBlock
                {
                    Text              = lane.Name,
                    Foreground        = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD)),
                    FontSize          = 12,
                    FontFamily        = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(12,0,4,0),
                    TextTrimming      = TextTrimming.CharacterEllipsis
                }
            };
            Canvas.SetLeft(lbl, 0);
            Canvas.SetTop(lbl, i * LaneH);
            _lanesC.Children.Add(lbl);
        }
    }

    // ── 시간축 ────────────────────────────────────────────────────────────────

    void DrawAxis(MainViewModel vm)
    {
        double ppd = vm.PixelsPerDay;

        // 레이블 패널 배경
        var lblBg = new Rectangle
        {
            Width  = LabelW,
            Height = AxisH,
            Fill   = new SolidColorBrush(LabelBg)
        };
        Canvas.SetLeft(lblBg, 0); Canvas.SetTop(lblBg, 0);
        _axisC.Children.Add(lblBg);

        // 전체 축 배경
        var axBg = new Rectangle
        {
            Width  = LabelW + (vm.VisibleDays + 14) * ppd,
            Height = AxisH,
            Fill   = new SolidColorBrush(AxisBg)
        };
        Canvas.SetLeft(axBg, 0); Canvas.SetTop(axBg, 0);
        _axisC.Children.Insert(0, axBg);

        // 세로 하단 보더
        var bdrLine = new Rectangle
        {
            Width  = LabelW + (vm.VisibleDays + 14) * ppd,
            Height = 1,
            Fill   = new SolidColorBrush(GridLine)
        };
        Canvas.SetLeft(bdrLine, 0); Canvas.SetTop(bdrLine, AxisH - 1);
        _axisC.Children.Add(bdrLine);

        // 날짜 눈금
        (string fmt, int step) = ppd switch
        {
            >= 30  => ("d일\nM월",  1),
            >= 10  => ("M/d",       3),
            >= 4   => ("M/d",       7),
            _      => ("yyyy\nM월", 30)
        };

        double totalDays = vm.VisibleDays + 14;
        for (int d = 0; d <= (int)totalDays; d += step)
        {
            var date = vm.ViewStart.AddDays(d);
            double x = LabelW + d * ppd;

            // 격자 세로선
            DrawLaneGridLine(x, vm.Lanes.Count, d % (step * 4) == 0);

            // 날짜 텍스트
            var tb = new TextBlock
            {
                Text              = date.ToString(step == 1 ? "M/d" : step <= 7 ? "M/d" : "yyyy\nM월"),
                FontSize          = 10,
                FontFamily        = new FontFamily("Segoe UI"),
                Foreground        = new SolidColorBrush(AxisText),
                TextAlignment     = TextAlignment.Center,
                Width             = step * ppd,
                TextWrapping      = TextWrapping.Wrap
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb,  4);
            _axisC.Children.Add(tb);
        }

        // 오늘 선
        double todayX = LabelW + (DateTime.Today - vm.ViewStart).TotalDays * ppd;
        if (todayX >= LabelW)
        {
            var todayLine = new Rectangle
            {
                Width  = 2,
                Height = AxisH + vm.Lanes.Count * LaneH,
                Fill   = new SolidColorBrush(TodayLine),
                Opacity = 0.7
            };
            Canvas.SetLeft(todayLine, todayX - 1);
            Canvas.SetTop(todayLine, 0);
            _axisC.Children.Add(todayLine);
        }
    }

    void DrawLaneGridLine(double x, int laneCount, bool major)
    {
        var line = new Rectangle
        {
            Width   = 1,
            Height  = laneCount * LaneH,
            Fill    = new SolidColorBrush(major
                ? Color.FromRgb(0x33, 0x33, 0x50)
                : GridLine),
            Opacity = major ? 0.8 : 0.5
        };
        Canvas.SetLeft(line, x);
        Canvas.SetTop(line,  0);
        _lanesC.Children.Add(line);
    }

    // ── 이벤트 블록 ────────────────────────────────────────────────────────────

    void DrawEvents(MainViewModel vm)
    {
        double ppd = vm.PixelsPerDay;

        foreach (var ev in vm.Events)
        {
            double x = LabelW + (ev.Start - vm.ViewStart).TotalDays * ppd;
            double w = Math.Max(ppd * 0.5,
                       ev.IsMilestone ? ppd * 0.6 : (ev.End - ev.Start).TotalDays * ppd - 4);
            double y = ev.LaneIndex * LaneH + LanePad;
            double h = LaneH - LanePad * 2;

            var block = new TimelineEventBlock(ev, ppd)
            {
                Width  = w,
                Height = h
            };
            Canvas.SetLeft(block, x + 2);
            Canvas.SetTop(block,  y);
            _eventsC.Children.Add(block);

            bool sel = vm.SelectedEvent == ev;
            block.SetSelected(sel);

            block.MouseLeftButtonDown += (s, e) =>
            {
                var b = (TimelineEventBlock)s!;
                vm.SelectedEvent = b.Event;
                RebuildAll();  // 선택 표시 갱신

                _dragging      = b;
                _dragStart     = e.GetPosition(_eventsC);
                _dragOrigStart = b.Event.Start;
                _dragResizing  = e.GetPosition(b).X > b.Width - 10;
                _draggingBlock = !_dragResizing;
                b.CaptureMouse();
                e.Handled = true;
            };
        }
    }

    // ── 마우스 이벤트 ─────────────────────────────────────────────────────────

    void OnEventsMouseDown(object sender, MouseButtonEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        // 이벤트 블록 위 클릭이 아닌 경우: 새 이벤트 생성 준비
        if (e.OriginalSource == _eventsC || e.OriginalSource is Rectangle)
        {
            vm.SelectedEvent = null;
            _isCreating   = true;
            _createStart  = e.GetPosition(_eventsC);
            _eventsC.CaptureMouse();
            e.Handled = true;
        }
    }

    void OnEventsMouseMove(object sender, MouseEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        // 드래그 이동/리사이즈
        if (_dragging != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos     = e.GetPosition(_eventsC);
            double dx   = pos.X - _dragStart.X;
            double ppd  = vm.PixelsPerDay;
            double ddelta = dx / ppd;

            if (_dragResizing)
            {
                var newEnd = _dragOrigStart.AddDays(
                    (pos.X - (LabelW + (_dragOrigStart - vm.ViewStart).TotalDays * ppd)) / ppd);
                if (newEnd > _dragging.Event.Start.AddHours(1))
                    _dragging.Event.End = newEnd.Date.AddDays(1);
            }
            else
            {
                var newStart = _dragOrigStart.AddDays(ddelta);
                var duration = _dragging.Event.End - _dragging.Event.Start;
                _dragging.Event.Start = newStart.Date;
                _dragging.Event.End   = _dragging.Event.Start + duration;
            }

            double x = LabelW + (_dragging.Event.Start - vm.ViewStart).TotalDays * ppd + 2;
            double w = Math.Max(ppd * 0.5, (_dragging.Event.End - _dragging.Event.Start).TotalDays * ppd - 4);
            Canvas.SetLeft(_dragging, x);
            _dragging.Width = w;
            e.Handled = true;
        }

        // 새 이벤트 생성 중 드래그
        if (_isCreating && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(_eventsC);
            if (_createRect == null)
            {
                _createRect = new Rectangle
                {
                    Fill    = new SolidColorBrush(Color.FromArgb(80, 0x3B, 0x82, 0xF6)),
                    Stroke  = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    StrokeThickness = 1.5,
                    Height  = LaneH - LanePad * 2,
                    RadiusX = 4, RadiusY = 4
                };
                _eventsC.Children.Add(_createRect);
                int laneIdx = Math.Max(0, (int)((_createStart.Y) / LaneH));
                Canvas.SetTop(_createRect, laneIdx * LaneH + LanePad);
            }
            double x1 = Math.Min(_createStart.X, pos.X);
            double x2 = Math.Max(_createStart.X, pos.X);
            Canvas.SetLeft(_createRect, x1);
            _createRect.Width = Math.Max(4, x2 - x1);
            e.Handled = true;
        }
    }

    void OnEventsMouseUp(object sender, MouseButtonEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        if (_dragging != null)
        {
            _dragging.ReleaseMouseCapture();
            _dragging    = null;
            _dragResizing = false;
            _draggingBlock = false;
        }

        if (_isCreating)
        {
            _eventsC.ReleaseMouseCapture();
            _isCreating = false;

            var pos    = e.GetPosition(_eventsC);
            double ppd = vm.PixelsPerDay;
            double x1  = Math.Min(_createStart.X, pos.X);
            double x2  = Math.Max(_createStart.X, pos.X);

            if (x2 - x1 > ppd * 0.3)
            {
                var start = vm.ViewStart.AddDays((x1 - LabelW) / ppd);
                var end   = vm.ViewStart.AddDays((x2 - LabelW) / ppd);
                int lane  = Math.Max(0, (int)((_createStart.Y) / LaneH));
                if (lane >= vm.Lanes.Count) lane = vm.Lanes.Count - 1;

                vm.AddEvent(start.Date, end.Date.AddDays(1), lane);
            }

            if (_createRect != null)
            {
                _eventsC.Children.Remove(_createRect);
                _createRect = null;
            }

            RebuildAll();
            e.Handled = true;
        }
    }

    // ── PNG 내보내기 ──────────────────────────────────────────────────────────

    public BitmapSource RenderToBitmap()
    {
        var sz = CalcSize();
        double dpi = 96;
        var rtb = new RenderTargetBitmap((int)sz.Width, (int)sz.Height, dpi, dpi, PixelFormats.Pbgra32);
        _root.Measure(sz);
        _root.Arrange(new Rect(0, 0, sz.Width, sz.Height));
        rtb.Render(_root);
        return rtb;
    }
}

// ── 이벤트 블록 시각 요소 ─────────────────────────────────────────────────────

sealed class TimelineEventBlock : Border
{
    public TimelineEvent Event { get; }
    readonly double _ppd;

    public TimelineEventBlock(TimelineEvent ev, double ppd)
    {
        Event = ev;
        _ppd  = ppd;
        BuildVisual();
    }

    void BuildVisual()
    {
        var color = ParseColor(Event.Color);
        CornerRadius = new CornerRadius(Event.IsMilestone ? Height / 2 : 5);
        Background   = new SolidColorBrush(Color.FromArgb(0xDD, color.R, color.G, color.B));
        BorderThickness = new Thickness(1);
        BorderBrush  = new SolidColorBrush(Color.FromArgb(0xFF, color.R, color.G, color.B));
        Cursor       = Cursors.Hand;
        ToolTip      = $"{Event.Title}\n{Event.Start:M/d} – {Event.End:M/d}";

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });

        var lbl = new TextBlock
        {
            Text              = Event.Title,
            Foreground        = new SolidColorBrush(Colors.White),
            FontSize          = 11,
            FontFamily        = new FontFamily("Segoe UI"),
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 2, 0),
            TextTrimming      = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        // 리사이즈 핸들
        var handle = new Border
        {
            Width       = 6,
            Background  = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Cursor      = Cursors.SizeWE
        };
        Grid.SetColumn(handle, 1);
        grid.Children.Add(handle);

        Child = grid;
    }

    public void SetSelected(bool selected)
    {
        BorderThickness = selected ? new Thickness(2) : new Thickness(1);
        if (selected)
            BorderBrush = new SolidColorBrush(Colors.White);
        else
        {
            var c = ParseColor(Event.Color);
            BorderBrush = new SolidColorBrush(c);
        }
    }

    static Color ParseColor(string hex)
    {
        try
        {
            if (hex.StartsWith('#') && hex.Length == 7)
                return Color.FromRgb(
                    Convert.ToByte(hex[1..3], 16),
                    Convert.ToByte(hex[3..5], 16),
                    Convert.ToByte(hex[5..7], 16));
        }
        catch { }
        return Color.FromRgb(0x3B, 0x82, 0xF6);
    }
}
