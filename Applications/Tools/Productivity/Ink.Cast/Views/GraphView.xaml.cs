using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using InkCast.Models;

namespace InkCast.Views;

/// <summary>Force-directed 그래프 뷰 (노트 연결망 시각화)</summary>
public partial class GraphView : UserControl
{
    // ── 이벤트 ────────────────────────────────────────────────
    public event Action<int>? NoteSelected;

    // ── 그래프 노드 내부 클래스 ────────────────────────────────
    private class GraphNode
    {
        public int NoteId { get; set; }
        public string Title { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public Ellipse? Shape { get; set; }
        public TextBlock? Label { get; set; }
        public bool IsDragging { get; set; }
    }

    // ── 필드 ─────────────────────────────────────────────────
    private readonly List<GraphNode> _nodes = [];
    private readonly List<(int SourceId, string TargetTitle)> _edges = [];
    private readonly DispatcherTimer _simTimer;

    private GraphNode? _draggingNode;
    private Point _dragStart;
    private bool _isPanning;
    private Point _panStart;
    private double _panStartX;
    private double _panStartY;

    private const double NodeRadius   = 18;
    private const double RepulseK     = 8000;
    private const double AttractK     = 0.03;
    private const double Damping      = 0.85;
    private const int    MaxIter      = 300;
    private int _simIter;

    // ── 생성자 ────────────────────────────────────────────────
    public GraphView()
    {
        InitializeComponent();
        _simTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _simTimer.Tick += SimulationStep;
        SizeChanged += (_, _) => CenterGraph();
    }

    // ── 공개 메서드 ──────────────────────────────────────────
    public void LoadGraph(List<Note> notes, List<(int SourceId, string SourceTitle, string TargetTitle)> links)
    {
        _simTimer.Stop();
        GraphCanvas.Children.Clear();
        _nodes.Clear();
        _edges.Clear();

        var rnd = new Random(42);
        foreach (var note in notes)
        {
            var node = new GraphNode
            {
                NoteId = note.Id,
                Title  = note.Title,
                X = rnd.NextDouble() * 600 - 300,
                Y = rnd.NextDouble() * 400 - 200,
            };
            _nodes.Add(node);
        }

        foreach (var (srcId, _, tgtTitle) in links)
            _edges.Add((srcId, tgtTitle));

        TxtGraphInfo.Text = $"{notes.Count}개 노트 · {links.Count}개 연결";

        RenderGraph();
        _simIter = 0;
        _simTimer.Start();
    }

    // ── 시뮬레이션 ──────────────────────────────────────────
    private void SimulationStep(object? sender, EventArgs e)
    {
        if (_simIter++ > MaxIter) { _simTimer.Stop(); return; }

        double temperature = Math.Max(0.5, 50.0 * (1.0 - _simIter / (double)MaxIter));

        // 척력
        for (int i = 0; i < _nodes.Count; i++)
        {
            for (int j = i + 1; j < _nodes.Count; j++)
            {
                var a = _nodes[i];
                var b = _nodes[j];
                if (a.IsDragging || b.IsDragging) continue;
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double d  = Math.Sqrt(dx * dx + dy * dy) + 0.01;
                double f  = RepulseK / (d * d);
                double fx = f * dx / d;
                double fy = f * dy / d;
                a.Vx -= fx; a.Vy -= fy;
                b.Vx += fx; b.Vy += fy;
            }
        }

        // 인력 (엣지)
        foreach (var (srcId, tgtTitle) in _edges)
        {
            var src = _nodes.FirstOrDefault(n => n.NoteId == srcId);
            var tgt = _nodes.FirstOrDefault(n => n.Title.Equals(tgtTitle, StringComparison.OrdinalIgnoreCase));
            if (src is null || tgt is null) continue;
            if (src.IsDragging || tgt.IsDragging) continue;
            double dx = tgt.X - src.X;
            double dy = tgt.Y - src.Y;
            double d  = Math.Sqrt(dx * dx + dy * dy) + 0.01;
            double f  = AttractK * d;
            double fx = f * dx / d;
            double fy = f * dy / d;
            src.Vx += fx; src.Vy += fy;
            tgt.Vx -= fx; tgt.Vy -= fy;
        }

        // 중심 인력
        foreach (var n in _nodes)
        {
            if (n.IsDragging) continue;
            n.Vx += -0.001 * n.X;
            n.Vy += -0.001 * n.Y;
        }

        // 위치 업데이트
        foreach (var n in _nodes)
        {
            if (n.IsDragging) continue;
            n.Vx *= Damping;
            n.Vy *= Damping;
            double maxSpeed = temperature;
            double speed = Math.Sqrt(n.Vx * n.Vx + n.Vy * n.Vy);
            if (speed > maxSpeed) { n.Vx = n.Vx / speed * maxSpeed; n.Vy = n.Vy / speed * maxSpeed; }
            n.X += n.Vx;
            n.Y += n.Vy;
        }

        UpdatePositions();
    }

    // ── 렌더링 ───────────────────────────────────────────────
    private void RenderGraph()
    {
        GraphCanvas.Children.Clear();

        // 엣지 선 먼저
        foreach (var (srcId, tgtTitle) in _edges)
        {
            var src = _nodes.FirstOrDefault(n => n.NoteId == srcId);
            var tgt = _nodes.FirstOrDefault(n => n.Title.Equals(tgtTitle, StringComparison.OrdinalIgnoreCase));
            if (src is null || tgt is null) continue;
            var line = new Line
            {
                Stroke          = new SolidColorBrush(Color.FromArgb(100, 124, 106, 244)),
                StrokeThickness = 1.5,
                Tag             = (srcId, tgtTitle),
            };
            GraphCanvas.Children.Add(line);
        }

        // 노드
        foreach (var node in _nodes)
        {
            // 외부 glow
            var glow = new Ellipse
            {
                Width  = (NodeRadius + 6) * 2,
                Height = (NodeRadius + 6) * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(30, 124, 106, 244)),
            };
            GraphCanvas.Children.Add(glow);

            // 노드 원
            var circle = new Ellipse
            {
                Width  = NodeRadius * 2,
                Height = NodeRadius * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(255, 30, 30, 60)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 124, 106, 244)),
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Tag    = node,
                ToolTip = node.Title,
            };
            circle.MouseLeftButtonDown += NodeCircle_MouseDown;
            circle.MouseLeftButtonUp   += NodeCircle_MouseUp;
            circle.MouseMove           += NodeCircle_MouseMove;
            circle.MouseEnter          += (s, _) => ((Ellipse)s!).Stroke = new SolidColorBrush(Colors.White);
            circle.MouseLeave          += (s, _) => ((Ellipse)s!).Stroke = new SolidColorBrush(Color.FromArgb(200, 124, 106, 244));
            GraphCanvas.Children.Add(circle);
            node.Shape = circle;

            // 텍스트 레이블
            var label = new TextBlock
            {
                Text       = node.Title.Length > 14 ? node.Title[..14] + "…" : node.Title,
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(210, 205, 214, 244)),
                TextAlignment = TextAlignment.Center,
                Width      = 100,
                IsHitTestVisible = false,
            };
            GraphCanvas.Children.Add(label);
            node.Label = label;
        }

        UpdatePositions();
    }

    private void UpdatePositions()
    {
        foreach (var node in _nodes)
        {
            if (node.Shape is null) continue;

            double cx = node.X;
            double cy = node.Y;

            Canvas.SetLeft(node.Shape, cx - NodeRadius);
            Canvas.SetTop(node.Shape, cy - NodeRadius);

            // glow
            int shapeIdx = GraphCanvas.Children.IndexOf(node.Shape);
            if (shapeIdx > 0 && GraphCanvas.Children[shapeIdx - 1] is Ellipse glow)
            {
                Canvas.SetLeft(glow, cx - NodeRadius - 6);
                Canvas.SetTop(glow, cy - NodeRadius - 6);
            }

            if (node.Label is not null)
            {
                Canvas.SetLeft(node.Label, cx - 50);
                Canvas.SetTop(node.Label, cy + NodeRadius + 3);
            }
        }

        // 엣지 업데이트
        foreach (System.Windows.UIElement child in GraphCanvas.Children)
        {
            if (child is not Line line) continue;
            if (line.Tag is not (int srcId, string tgtTitle)) continue;
            var src = _nodes.FirstOrDefault(n => n.NoteId == srcId);
            var tgt = _nodes.FirstOrDefault(n => n.Title.Equals(tgtTitle, StringComparison.OrdinalIgnoreCase));
            if (src is null || tgt is null) continue;
            line.X1 = src.X; line.Y1 = src.Y;
            line.X2 = tgt.X; line.Y2 = tgt.Y;
        }
    }

    // ── 노드 드래그 ──────────────────────────────────────────
    private void NodeCircle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse circle || circle.Tag is not GraphNode node) return;
        _draggingNode = node;
        node.IsDragging = true;
        _dragStart = e.GetPosition(GraphCanvas);
        circle.CaptureMouse();
        e.Handled = true;
    }

    private void NodeCircle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingNode is null || sender is not Ellipse circle) return;
        var pos = e.GetPosition(GraphCanvas);
        _draggingNode.X = pos.X;
        _draggingNode.Y = pos.Y;
        UpdatePositions();
        e.Handled = true;
    }

    private void NodeCircle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingNode is null) return;
        var pos = e.GetPosition(GraphCanvas);
        double d = Math.Sqrt(Math.Pow(pos.X - _dragStart.X, 2) + Math.Pow(pos.Y - _dragStart.Y, 2));
        if (d < 5) NoteSelected?.Invoke(_draggingNode.NoteId); // 클릭 → 노트 선택
        _draggingNode.IsDragging = false;
        _draggingNode.Vx = 0; _draggingNode.Vy = 0;
        _draggingNode = null;
        if (sender is UIElement el) el.ReleaseMouseCapture();
        e.Handled = true;
    }

    // ── 캔버스 팬 ────────────────────────────────────────────
    private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_draggingNode is not null) return;
        _isPanning = true;
        _panStart = e.GetPosition(this);
        _panStartX = TranslateT.X;
        _panStartY = TranslateT.Y;
        GraphCanvas.CaptureMouse();
    }

    private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        GraphCanvas.ReleaseMouseCapture();
    }

    private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var pos = e.GetPosition(this);
        TranslateT.X = _panStartX + (pos.X - _panStart.X);
        TranslateT.Y = _panStartY + (pos.Y - _panStart.Y);
    }

    // ── 줌 ──────────────────────────────────────────────────
    private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.12 : 0.89;
        ApplyZoom(factor, e.GetPosition(GraphBorder));
    }

    private void ApplyZoom(double factor, Point center)
    {
        double newScale = Math.Clamp(ScaleT.ScaleX * factor, 0.2, 5.0);
        double ratio    = newScale / ScaleT.ScaleX;
        TranslateT.X = center.X + (TranslateT.X - center.X) * ratio;
        TranslateT.Y = center.Y + (TranslateT.Y - center.Y) * ratio;
        ScaleT.ScaleX = newScale;
        ScaleT.ScaleY = newScale;
    }

    // ── 툴바 버튼 ────────────────────────────────────────────
    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        => ApplyZoom(1.25, new Point(ActualWidth / 2, ActualHeight / 2));

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        => ApplyZoom(0.8, new Point(ActualWidth / 2, ActualHeight / 2));

    private void BtnFit_Click(object sender, RoutedEventArgs e) => CenterGraph();

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        var rnd = new Random();
        foreach (var n in _nodes)
        {
            n.X = rnd.NextDouble() * 600 - 300;
            n.Y = rnd.NextDouble() * 400 - 200;
            n.Vx = n.Vy = 0;
        }
        _simIter = 0;
        _simTimer.Start();
    }

    private void CenterGraph()
    {
        ScaleT.ScaleX = ScaleT.ScaleY = 1;
        TranslateT.X = ActualWidth / 2;
        TranslateT.Y = ActualHeight / 2;
    }
}
