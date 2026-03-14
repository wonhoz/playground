using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CircuitBreak.Models;
using CircuitBreak.Services;

namespace CircuitBreak;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private List<PuzzleLevel> _levels = [];
    private int _currentLevelIdx = 0;
    private PuzzleLevel? _level;
    private SimulationResult? _lastResult;

    private MeterMode _meterMode = MeterMode.Voltmeter;
    private Component? _selectedComp;

    // Canvas UI 요소 참조
    private readonly Dictionary<int, UIElement> _compShapes = [];
    private readonly Dictionary<int, TextBlock> _nodeLabels = [];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _levels = LevelFactory.CreateAll();
        LoadLevel(0);
    }

    // ─── 레벨 관리 ────────────────────────────────────────────────────────
    void LoadLevel(int idx)
    {
        if (idx < 0 || idx >= _levels.Count) return;
        _currentLevelIdx = idx;
        _level = DeepCloneLevel(_levels[idx]);
        _selectedComp = null;
        _lastResult = null;

        LevelTitle.Text = $"Level {_level.Number}: {_level.Name}";
        DescText.Text = _level.Description;
        FixPanel.Visibility = Visibility.Collapsed;
        SolveStatus.Text = "";

        UpdateTargetList();
        UpdateBugCount();
        DrawCircuit();
        RunSimulation();
    }

    void BtnPrev_Click(object sender, RoutedEventArgs e) => LoadLevel(_currentLevelIdx - 1);
    void BtnNext_Click(object sender, RoutedEventArgs e) => LoadLevel(_currentLevelIdx + 1);
    void BtnReset_Click(object sender, RoutedEventArgs e) => LoadLevel(_currentLevelIdx);

    // ─── 회로도 렌더링 ────────────────────────────────────────────────────
    void DrawCircuit()
    {
        CircuitCanvas.Children.Clear();
        _compShapes.Clear();
        _nodeLabels.Clear();
        if (_level == null) return;

        // 소자 그리기
        foreach (var comp in _level.Components)
        {
            if (comp.Type == ComponentType.Battery) continue;
            DrawComponent(comp);
        }

        // 노드 그리기
        foreach (var node in _level.Nodes)
            DrawNode(node);

        UpdateNodeVoltageLabels();
    }

    void DrawComponent(Component comp)
    {
        if (_level == null) return;
        var nodeA = _level.Nodes.First(n => n.Id == comp.NodeA);
        var nodeB = _level.Nodes.First(n => n.Id == comp.NodeB);

        double x1 = nodeA.Position.X, y1 = nodeA.Position.Y;
        double x2 = nodeB.Position.X, y2 = nodeB.Position.Y;
        double mx = (x1 + x2) / 2, my = (y1 + y2) / 2;

        // 색상 결정
        Brush color = comp.IsFixed ? Brushes.LimeGreen
            : comp.IsBug ? new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
            : new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));

        // 선
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = color, StrokeThickness = comp.IsBug ? 2 : 1.5,
            StrokeDashArray = comp.Type == ComponentType.BrokenWire && !comp.IsFixed
                ? new DoubleCollection([4, 4]) : null,
            Cursor = Cursors.Hand, Tag = comp
        };
        line.MouseDown += Comp_MouseDown;
        CircuitCanvas.Children.Add(line);

        // 소자 기호
        var rect = new Rectangle
        {
            Width = 40, Height = 16,
            Fill = comp.IsFixed ? new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A))
                : comp.IsBug ? new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
            Stroke = color, StrokeThickness = 1.5,
            RadiusX = 3, RadiusY = 3,
            Cursor = Cursors.Hand, Tag = comp
        };
        Canvas.SetLeft(rect, mx - 20);
        Canvas.SetTop(rect, my - 8);
        rect.MouseDown += Comp_MouseDown;
        CircuitCanvas.Children.Add(rect);
        _compShapes[comp.Id] = rect;

        // 레이블
        var lbl = new TextBlock
        {
            Text = comp.Label, FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(lbl, mx - 20);
        Canvas.SetTop(lbl, my - 22);
        CircuitCanvas.Children.Add(lbl);
    }

    void DrawNode(Node node)
    {
        var ellipse = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = node.IsGround ? Brushes.Gray
                : node.IsVoltageSource ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F))
                : Brushes.White,
            Stroke = Brushes.DimGray, StrokeThickness = 1
        };
        Canvas.SetLeft(ellipse, node.Position.X - 5);
        Canvas.SetTop(ellipse, node.Position.Y - 5);
        CircuitCanvas.Children.Add(ellipse);

        var lbl = new TextBlock
        {
            Text = node.IsGround ? "GND" : node.IsVoltageSource ? $"{_level!.SourceVoltage}V" : $"N{node.Id}",
            FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };
        Canvas.SetLeft(lbl, node.Position.X + 6);
        Canvas.SetTop(lbl, node.Position.Y - 14);
        CircuitCanvas.Children.Add(lbl);
        _nodeLabels[node.Id] = lbl;
    }

    void UpdateNodeVoltageLabels()
    {
        if (_lastResult == null || !_lastResult.IsValid) return;
        foreach (var (nodeId, voltage) in _lastResult.NodeVoltages)
        {
            if (_nodeLabels.TryGetValue(nodeId, out var lbl))
            {
                var node = _level!.Nodes.First(n => n.Id == nodeId);
                string prefix = node.IsGround ? "GND" : node.IsVoltageSource ? "+" : $"N{nodeId}";
                lbl.Text = $"{prefix}: {voltage:F2}V";
                lbl.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
            }
        }
    }

    // ─── 소자 클릭 ────────────────────────────────────────────────────────
    void Comp_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Component comp)
            SelectComponent(comp);
    }

    void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 빈 캔버스 클릭 → 선택 해제
        SelectComponent(null);
    }

    void SelectComponent(Component? comp)
    {
        _selectedComp = comp;
        if (comp == null)
        {
            FixPanel.Visibility = Visibility.Collapsed;
            StatusBar.Text = "소자를 클릭해 검사하거나 수정하세요.";
            MeasureSelected();
            return;
        }

        MeasureSelected();

        if (comp.IsBug && !comp.IsFixed)
        {
            FixPanel.Visibility = Visibility.Visible;
            FixTitle.Text = $"버그 발견: {comp.Label}";
            FixDesc.Text = comp.Type switch
            {
                ComponentType.BrokenWire => "도선이 단선되어 있습니다. 수리하면 전류가 흐릅니다.",
                ComponentType.ShortCircuit => "단락이 발생해 저항이 매우 낮습니다. 제거하면 정상화됩니다.",
                ComponentType.WrongResistance => $"저항값이 잘못됨. 현재: {comp.BugValue}Ω → 정상: {comp.Value}Ω",
                _ => "이 소자에 버그가 있습니다."
            };
        }
        else
        {
            FixPanel.Visibility = Visibility.Collapsed;
        }

        StatusBar.Text = $"선택: {comp.Label} (NodeA={comp.NodeA}, NodeB={comp.NodeB})";
    }

    // ─── 측정 도구 ────────────────────────────────────────────────────────
    void BtnVoltmeter_Click(object sender, RoutedEventArgs e) { _meterMode = MeterMode.Voltmeter; MeasureSelected(); }
    void BtnOhmmeter_Click(object sender, RoutedEventArgs e) { _meterMode = MeterMode.Ohmmeter; MeasureSelected(); }
    void BtnAmmeter_Click(object sender, RoutedEventArgs e) { _meterMode = MeterMode.Ammeter; MeasureSelected(); }

    void MeasureSelected()
    {
        if (_selectedComp == null || _lastResult == null)
        {
            MeterDisplay.Text = "—";
            MeterUnit.Text = "";
            return;
        }

        switch (_meterMode)
        {
            case MeterMode.Voltmeter:
                double va = _lastResult.NodeVoltages.GetValueOrDefault(_selectedComp.NodeA, 0);
                double vb = _lastResult.NodeVoltages.GetValueOrDefault(_selectedComp.NodeB, 0);
                MeterDisplay.Text = $"{va - vb:F3}";
                MeterUnit.Text = "V (A-B 전압)";
                break;

            case MeterMode.Ohmmeter:
                double r = _selectedComp.EffectiveValue;
                if (_selectedComp.Type == ComponentType.Resistor || _selectedComp.Type == ComponentType.WrongResistance)
                {
                    MeterDisplay.Text = r >= 1000 ? $"{r / 1000:F1}k" : $"{r:F0}";
                    MeterUnit.Text = "Ω";
                }
                else
                {
                    MeterDisplay.Text = _selectedComp.Type == ComponentType.BrokenWire && !_selectedComp.IsFixed ? "∞" : "~0";
                    MeterUnit.Text = "Ω";
                }
                break;

            case MeterMode.Ammeter:
                if (_lastResult.BranchCurrents.TryGetValue(_selectedComp.Id, out double current))
                {
                    double mA = current * 1000;
                    MeterDisplay.Text = $"{mA:F3}";
                    MeterUnit.Text = "mA";
                }
                else
                {
                    MeterDisplay.Text = "—"; MeterUnit.Text = "";
                }
                break;
        }
    }

    // ─── 수정 ─────────────────────────────────────────────────────────────
    void BtnFix_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedComp == null || !_selectedComp.IsBug || _selectedComp.IsFixed) return;
        _selectedComp.IsFixed = true;
        FixPanel.Visibility = Visibility.Collapsed;
        StatusBar.Text = $"✅ '{_selectedComp.Label}' 수정 완료!";
        UpdateBugCount();
        DrawCircuit();
        RunSimulation();
    }

    // ─── 시뮬레이션 ───────────────────────────────────────────────────────
    void BtnSimulate_Click(object sender, RoutedEventArgs e) => RunSimulation();

    void RunSimulation()
    {
        if (_level == null) return;
        _lastResult = CircuitSimulator.Simulate(_level);

        var items = new List<string>();
        if (_lastResult.IsValid)
        {
            foreach (var (nodeId, v) in _lastResult.NodeVoltages.OrderBy(kv => kv.Key))
            {
                var node = _level.Nodes.FirstOrDefault(n => n.Id == nodeId);
                string name = node?.IsGround == true ? "GND" : node?.IsVoltageSource == true ? "VCC" : $"N{nodeId}";
                items.Add($"{name}: {v:F3}V");
            }
            items.Add($"총 전류: {_lastResult.TotalCurrent * 1000:F3}mA");
        }
        else
        {
            items.Add($"오류: {_lastResult.ErrorMessage}");
        }
        ResultList.ItemsSource = items;

        UpdateNodeVoltageLabels();
        MeasureSelected();
        CheckSolution();
    }

    void CheckSolution()
    {
        if (_level == null || _lastResult == null) return;
        bool solved = CircuitSimulator.CheckSolution(_level, _lastResult);
        bool allFixed = _level.Components.Where(c => c.IsBug).All(c => c.IsFixed);

        if (solved && allFixed)
        {
            SolveStatus.Text = "🎉 완벽! 모든 버그 수정 완료!";
            SolveStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
        }
        else if (solved)
        {
            SolveStatus.Text = "✅ 회로 정상 동작 (숨은 버그 있음)";
            SolveStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F));
        }
        else
        {
            SolveStatus.Text = "❌ 목표 미달성";
            SolveStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        }
    }

    // ─── UI 헬퍼 ─────────────────────────────────────────────────────────
    void UpdateTargetList()
    {
        if (_level == null) return;
        var targets = _level.TargetVoltages
            .Select(kv => $"N{kv.Key}: {kv.Value:F2}V 목표")
            .ToList();
        if (_level.TargetCurrentAmps.HasValue)
            targets.Add($"전류: {_level.TargetCurrentAmps.Value * 1000:F2}mA 목표");
        TargetList.ItemsSource = targets;
    }

    void UpdateBugCount()
    {
        if (_level == null) return;
        int total = _level.Components.Count(c => c.IsBug);
        int fixed_ = _level.Components.Count(c => c.IsBug && c.IsFixed);
        BugCount.Text = $"버그 {fixed_}/{total} 수정";
    }

    // ─── 딥 클론 (레벨 리셋용) ───────────────────────────────────────────
    static PuzzleLevel DeepCloneLevel(PuzzleLevel src) => new()
    {
        Number = src.Number,
        Name = src.Name,
        Description = src.Description,
        SourceNodeId = src.SourceNodeId,
        GroundNodeId = src.GroundNodeId,
        SourceVoltage = src.SourceVoltage,
        TargetVoltages = new Dictionary<int, double>(src.TargetVoltages),
        TargetCurrentAmps = src.TargetCurrentAmps,
        Tolerance = src.Tolerance,
        Nodes = src.Nodes.Select(n => new Node
        {
            Id = n.Id, Position = n.Position,
            IsGround = n.IsGround, IsVoltageSource = n.IsVoltageSource
        }).ToList(),
        Components = src.Components.Select(c => new Component
        {
            Id = c.Id, Type = c.Type, NodeA = c.NodeA, NodeB = c.NodeB,
            Value = c.Value, BugValue = c.BugValue, IsBug = c.IsBug,
            IsFixed = false, Label = c.Label
        }).ToList()
    };
}
