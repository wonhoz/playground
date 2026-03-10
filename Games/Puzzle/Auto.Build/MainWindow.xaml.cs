using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AutoBuild;

// ── 열거형 ──────────────────────────────────────────────────────────────

enum Dir { Right, Down, Left, Up }

enum MachineType
{
    None,
    Spawner,    // 원료 생성 (고정)
    BeltR, BeltD, BeltL, BeltU,  // 컨베이어 벨트 (방향)
    Processor,  // 원료 → 가공품 (Yellow)
    Sorter,     // 지정 색상 → 우, 나머지 → 진행방향
    Merger,     // 두 입력 → 합성 (Purple)
    Collector,  // 목표 수집 (고정)
}

enum ItemColor { Red, Green, Blue, Yellow, Purple }

// ── 데이터 클래스 ────────────────────────────────────────────────────────

class Cell
{
    public MachineType Type;
    public ItemColor SpawnColor;    // Spawner 색상
    public ItemColor FilterColor;  // Sorter 필터 색상
    public bool Locked;             // 고정 셀 (Spawner/Collector)
    public Dir Direction => Type switch
    {
        MachineType.BeltR => Dir.Right,
        MachineType.BeltD => Dir.Down,
        MachineType.BeltL => Dir.Left,
        MachineType.BeltU => Dir.Up,
        _ => Dir.Right,
    };
}

class Item
{
    public int X, Y;
    public ItemColor Color;
    public bool Processing;   // Processor 안에서 가공 중
    public int ProcessLeft;   // 남은 가공 틱
    public bool Merging;      // Merger 입력 대기 중
    public bool Moved;        // 이번 틱 이미 이동함
}

// ── 스테이지 정의 ────────────────────────────────────────────────────────

class StageDef
{
    public string Name = "";
    public string Description = "";
    public int GoalCount;
    public ItemColor GoalColor;
    public int SpawnIntervalTicks = 4;  // 몇 틱마다 스폰

    // 고정 셀 목록: (x, y, type, color)
    public List<(int x, int y, MachineType type, ItemColor color)> Fixed = [];
}

// ── 메인 윈도우 ──────────────────────────────────────────────────────────

public partial class MainWindow : Window
{
    // 그리드 설정
    private const int COLS = 12;
    private const int ROWS = 8;
    private const int CELL = 58;
    private const int PROCESS_TICKS = 3;  // 가공에 필요한 틱 수
    private const int MERGE_TICKS   = 2;  // 합성 대기 틱

    // 게임 상태
    private Cell[,] _grid = new Cell[COLS, ROWS];
    private List<Item> _items = [];
    private List<Item> _itemsToRemove = [];

    private bool _running;
    private int _tick;
    private int _speedIndex = 1;  // 0=0.5×, 1=1×, 2=2×, 3=4×
    private readonly int[] _speeds = [1000, 500, 250, 125];  // ms per tick
    private int _collected;
    private DateTime _startTime;
    private int _spawnTick;

    private DispatcherTimer _timer = new();

    // 선택된 도구
    private MachineType _selectedTool = MachineType.BeltR;
    private ItemColor _sorterFilter = ItemColor.Red;

    // 스테이지
    private int _stageIndex;
    private bool _sandboxMode;
    private readonly List<StageDef> _stages;

    // 색상 테이블
    private static readonly Dictionary<ItemColor, Color> ItemColors = new()
    {
        [ItemColor.Red]    = Color.FromRgb(0xFF, 0x50, 0x50),
        [ItemColor.Green]  = Color.FromRgb(0x50, 0xFF, 0x50),
        [ItemColor.Blue]   = Color.FromRgb(0x50, 0x80, 0xFF),
        [ItemColor.Yellow] = Color.FromRgb(0xFF, 0xD0, 0x50),
        [ItemColor.Purple] = Color.FromRgb(0xFF, 0x80, 0xFF),
    };

    private static readonly string[] ItemLabels = ["R", "G", "B", "Y", "P"];

    public MainWindow()
    {
        InitializeComponent();
        _stages = BuildStages();
        InitGrid();
        Loaded += (_, _) => RenderGrid();
    }

    // ── 스테이지 정의 ──────────────────────────────────────────────────

    private static List<StageDef> BuildStages() =>
    [
        new StageDef
        {
            Name = "기초 이송",
            Description = "빨간 원료를 컨베이어 벨트로 수거함까지 옮기세요.\n벨트를 배치해 경로를 연결하세요!",
            GoalCount = 10,
            GoalColor = ItemColor.Red,
            SpawnIntervalTicks = 4,
            Fixed =
            [
                (0, 3, MachineType.Spawner,   ItemColor.Red),
                (11,3, MachineType.Collector, ItemColor.Red),
            ],
        },
        new StageDef
        {
            Name = "가공 체인",
            Description = "빨간 원료를 ⚙가공기에 통과시켜\n노란 가공품을 수거함으로 보내세요.",
            GoalCount = 8,
            GoalColor = ItemColor.Yellow,
            SpawnIntervalTicks = 5,
            Fixed =
            [
                (0, 3, MachineType.Spawner,   ItemColor.Red),
                (11,3, MachineType.Collector, ItemColor.Yellow),
            ],
        },
        new StageDef
        {
            Name = "색상 분류",
            Description = "⊕분류기로 빨강/초록을 나눠\n각각 다른 수거함으로 보내세요.",
            GoalCount = 12,
            GoalColor = ItemColor.Red,  // 두 색 모두 카운트
            SpawnIntervalTicks = 3,
            Fixed =
            [
                (0, 1, MachineType.Spawner,   ItemColor.Red),
                (0, 5, MachineType.Spawner,   ItemColor.Green),
                (11,1, MachineType.Collector, ItemColor.Red),
                (11,6, MachineType.Collector, ItemColor.Green),
            ],
        },
        new StageDef
        {
            Name = "합성 라인",
            Description = "빨강+파랑을 ✕합성기에서 합쳐\n보라 합성품을 수거함으로 보내세요.",
            GoalCount = 6,
            GoalColor = ItemColor.Purple,
            SpawnIntervalTicks = 4,
            Fixed =
            [
                (0, 2, MachineType.Spawner,   ItemColor.Red),
                (0, 5, MachineType.Spawner,   ItemColor.Blue),
                (11,3, MachineType.Collector, ItemColor.Purple),
            ],
        },
        new StageDef
        {
            Name = "종합 공장",
            Description = "가공+분류+합성을 모두 활용해\n노란 가공품과 보라 합성품을 각각 수집!",
            GoalCount = 10,
            GoalColor = ItemColor.Yellow,  // 노랑+보라 합계
            SpawnIntervalTicks = 3,
            Fixed =
            [
                (0, 1, MachineType.Spawner,   ItemColor.Red),
                (0, 4, MachineType.Spawner,   ItemColor.Blue),
                (0, 6, MachineType.Spawner,   ItemColor.Green),
                (11,1, MachineType.Collector, ItemColor.Yellow),
                (11,5, MachineType.Collector, ItemColor.Purple),
            ],
        },
    ];

    // ── 그리드 초기화 ──────────────────────────────────────────────────

    private void InitGrid()
    {
        _items.Clear();
        _tick = 0;
        _collected = 0;
        _spawnTick = 0;
        _running = false;

        _grid = new Cell[COLS, ROWS];
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
                _grid[x, y] = new Cell();

        if (!_sandboxMode && _stageIndex < _stages.Count)
        {
            var stage = _stages[_stageIndex];
            foreach (var (fx, fy, ftype, fcolor) in stage.Fixed)
            {
                if (fx < 0 || fx >= COLS || fy < 0 || fy >= ROWS) continue;
                _grid[fx, fy].Type = ftype;
                _grid[fx, fy].SpawnColor = fcolor;
                _grid[fx, fy].Locked = true;
                _grid[fx, fy].FilterColor = fcolor;
            }
        }

        UpdateUI();
    }

    // ── 렌더링 ────────────────────────────────────────────────────────

    private void RenderGrid()
    {
        GameCanvas.Children.Clear();

        // 캔버스 크기를 그리드에 맞게
        double totalW = COLS * CELL;
        double totalH = ROWS * CELL;
        GameCanvas.Width  = totalW;
        GameCanvas.Height = totalH;

        // 부모 Border에 맞게 오프셋 계산
        double offX = (((Border)GameCanvas.Parent).ActualWidth  - totalW) / 2;
        double offY = (((Border)GameCanvas.Parent).ActualHeight - totalH) / 2;
        Canvas.SetLeft(GameCanvas, Math.Max(0, offX));
        Canvas.SetTop (GameCanvas, Math.Max(0, offY));

        DrawGrid();
        RedrawItems();
    }

    private void DrawGrid()
    {
        GameCanvas.Children.Clear();

        for (int x = 0; x < COLS; x++)
        {
            for (int y = 0; y < ROWS; y++)
            {
                double px = x * CELL;
                double py = y * CELL;

                var cell = _grid[x, y];

                // 셀 배경
                var bg = new Rectangle
                {
                    Width  = CELL - 1,
                    Height = CELL - 1,
                    Fill   = GetCellBrush(cell),
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A)),
                    StrokeThickness = 1,
                    RadiusX = 3, RadiusY = 3,
                };
                Canvas.SetLeft(bg, px); Canvas.SetTop(bg, py);
                GameCanvas.Children.Add(bg);

                // 셀 레이블
                string label = GetCellLabel(cell);
                if (label != "")
                {
                    var tb = new TextBlock
                    {
                        Text       = label,
                        FontSize   = cell.Type is MachineType.Spawner or MachineType.Collector ? 10 : 16,
                        Foreground = GetCellFg(cell),
                        TextAlignment = TextAlignment.Center,
                        Width      = CELL - 1,
                    };
                    Canvas.SetLeft(tb, px);
                    Canvas.SetTop (tb, py + (CELL - 1) / 2.0 - 12);
                    GameCanvas.Children.Add(tb);

                    // 색상 힌트 (Spawner/Collector)
                    if (cell.Type is MachineType.Spawner or MachineType.Collector)
                    {
                        var dot = new Ellipse
                        {
                            Width  = 10, Height = 10,
                            Fill   = new SolidColorBrush(ItemColors[cell.SpawnColor]),
                        };
                        Canvas.SetLeft(dot, px + (CELL - 1) / 2.0 - 5);
                        Canvas.SetTop (dot, py + (CELL - 1) - 14);
                        GameCanvas.Children.Add(dot);
                    }
                }
            }
        }

        RedrawItems();
    }

    private void RedrawItems()
    {
        // 기존 아이템 shape 제거
        var toRemove = GameCanvas.Children.OfType<UIElement>()
            .Where(e => e is Ellipse el && (string?)el.Tag == "item" ||
                        e is TextBlock tb && (string?)tb.Tag == "item-label")
            .ToList();
        foreach (var e in toRemove) GameCanvas.Children.Remove(e);

        foreach (var item in _items)
        {
            double px = item.X * CELL + (CELL - 1) / 2.0;
            double py = item.Y * CELL + (CELL - 1) / 2.0;
            double r  = item.Processing ? 14 : 12;

            var ellipse = new Ellipse
            {
                Width  = r * 2, Height = r * 2,
                Fill   = new SolidColorBrush(ItemColors[item.Color]),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = item.Processing ? 2 : 1,
                Opacity = item.Processing ? 0.7 : 1.0,
                Tag = "item",
            };
            Canvas.SetLeft(ellipse, px - r);
            Canvas.SetTop (ellipse, py - r);
            GameCanvas.Children.Add(ellipse);

            // 가공 중 표시
            if (item.Processing)
            {
                var lbl = new TextBlock
                {
                    Text       = "⚙",
                    FontSize   = 9,
                    Foreground = Brushes.White,
                    Tag        = "item-label",
                };
                Canvas.SetLeft(lbl, px - 5);
                Canvas.SetTop (lbl, py - 6);
                GameCanvas.Children.Add(lbl);
            }
        }
    }

    private static Brush GetCellBrush(Cell c) => c.Type switch
    {
        MachineType.None      => new SolidColorBrush(Color.FromRgb(0x10, 0x20, 0x10)),
        MachineType.Spawner   => new SolidColorBrush(Color.FromRgb(0x20, 0x40, 0x15)),
        MachineType.Collector => new SolidColorBrush(Color.FromRgb(0x40, 0x25, 0x10)),
        MachineType.BeltR or MachineType.BeltD or MachineType.BeltL or MachineType.BeltU
                              => new SolidColorBrush(Color.FromRgb(0x15, 0x30, 0x15)),
        MachineType.Processor => new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x10)),
        MachineType.Sorter    => new SolidColorBrush(Color.FromRgb(0x10, 0x25, 0x35)),
        MachineType.Merger    => new SolidColorBrush(Color.FromRgb(0x30, 0x10, 0x30)),
        _ => Brushes.Transparent,
    };

    private static string GetCellLabel(Cell c) => c.Type switch
    {
        MachineType.None      => "",
        MachineType.Spawner   => "SPAWN",
        MachineType.Collector => "GOAL",
        MachineType.BeltR     => "→",
        MachineType.BeltD     => "↓",
        MachineType.BeltL     => "←",
        MachineType.BeltU     => "↑",
        MachineType.Processor => "⚙",
        MachineType.Sorter    => "⊕",
        MachineType.Merger    => "✕",
        _ => "",
    };

    private static Brush GetCellFg(Cell c) => c.Type switch
    {
        MachineType.Spawner   => new SolidColorBrush(Color.FromRgb(0x80, 0xFF, 0x80)),
        MachineType.Collector => new SolidColorBrush(Color.FromRgb(0xFF, 0xA0, 0x50)),
        MachineType.Processor => new SolidColorBrush(Color.FromRgb(0xFF, 0xD0, 0x50)),
        MachineType.Sorter    => new SolidColorBrush(Color.FromRgb(0x50, 0xC0, 0xFF)),
        MachineType.Merger    => new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0xFF)),
        _ => new SolidColorBrush(Color.FromRgb(0x60, 0xB0, 0x60)),
    };

    // ── 시뮬레이션 틱 ─────────────────────────────────────────────────

    private void Tick()
    {
        _tick++;
        _spawnTick++;

        var stage    = (!_sandboxMode && _stageIndex < _stages.Count) ? _stages[_stageIndex] : null;
        int interval = stage?.SpawnIntervalTicks ?? 5;

        // 1. 스포너: 새 아이템 생성
        if (_spawnTick >= interval)
        {
            _spawnTick = 0;
            for (int x = 0; x < COLS; x++)
                for (int y = 0; y < ROWS; y++)
                {
                    var c = _grid[x, y];
                    if (c.Type != MachineType.Spawner) continue;
                    // 해당 셀에 아이템 없을 때만 생성
                    if (!_items.Any(i => i.X == x && i.Y == y))
                        _items.Add(new Item { X = x, Y = y, Color = c.SpawnColor });
                }
        }

        // 2. 아이템 이동 (이번 틱 플래그 초기화)
        foreach (var item in _items) item.Moved = false;

        _itemsToRemove.Clear();

        // 처리: 가공 중 아이템 틱 감소
        foreach (var item in _items.Where(i => i.Processing))
        {
            item.ProcessLeft--;
            if (item.ProcessLeft <= 0)
            {
                item.Processing = false;
                item.Color = ItemColor.Yellow;
            }
        }

        // 3. 이동 처리 (Collector → Processor → Belt 순으로)
        MoveItems();

        // 4. 렌더링
        DrawGrid();
        UpdateUI();

        // 5. 클리어 체크
        CheckClear();
    }

    private void MoveItems()
    {
        // 머저 합성 처리
        ProcessMergers();

        // 이동: 아직 이동 안 한 아이템만, 가공 중 제외
        var movable = _items.Where(i => !i.Moved && !i.Processing && !i.Merging).ToList();

        foreach (var item in movable)
        {
            var cell = _grid[item.X, item.Y];

            // Collector
            if (cell.Type == MachineType.Collector)
            {
                if (IsGoalColor(item.Color, cell.SpawnColor))
                    _collected++;
                _itemsToRemove.Add(item);
                item.Moved = true;
                continue;
            }

            // Processor: 아이템이 들어오면 가공 시작
            if (cell.Type == MachineType.Processor && !item.Processing)
            {
                if (item.Color != ItemColor.Yellow && item.Color != ItemColor.Purple)
                {
                    item.Processing  = true;
                    item.ProcessLeft = PROCESS_TICKS;
                    item.Moved = true;
                    continue;
                }
            }

            // Belt/기타: 방향으로 이동
            Dir dir = GetOutDir(cell, item);
            var (nx, ny) = Move(item.X, item.Y, dir);

            if (!InBounds(nx, ny)) { _itemsToRemove.Add(item); item.Moved = true; continue; }

            // 목적지에 이미 아이템 있으면 대기
            if (_items.Any(i => i != item && i.X == nx && i.Y == ny && !_itemsToRemove.Contains(i)))
            {
                item.Moved = true;
                continue;
            }

            item.X = nx; item.Y = ny;
            item.Moved = true;
        }

        foreach (var r in _itemsToRemove) _items.Remove(r);
        _itemsToRemove.Clear();
    }

    private void ProcessMergers()
    {
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
            {
                if (_grid[x, y].Type != MachineType.Merger) continue;

                // 이 셀의 아이템들
                var here = _items.Where(i => i.X == x && i.Y == y && !i.Moved).ToList();
                if (here.Count < 2) continue;

                // 두 개를 합성
                var a = here[0];
                var b = here[1];
                a.Color      = ItemColor.Purple;
                a.Moved      = false;
                a.Merging    = false;
                _itemsToRemove.Add(b);
            }
    }

    private bool IsGoalColor(ItemColor item, ItemColor goal)
    {
        if (_sandboxMode) return true;
        if (_stageIndex == 2)  // 분류 스테이지: 빨강+초록 모두
            return item == goal;
        if (_stageIndex == 4)  // 종합: 노랑+보라
            return item is ItemColor.Yellow or ItemColor.Purple;
        return item == goal;
    }

    private static Dir GetOutDir(Cell cell, Item item)
    {
        if (cell.Type == MachineType.Sorter)
        {
            // 필터 색상 매칭 → 오른쪽(기준 방향 90도 회전), 나머지 → 직진(아래)
            // 단순화: 필터 매칭이면 Right, 아니면 Down
            return item.Color == cell.FilterColor ? Dir.Right : Dir.Down;
        }
        return cell.Direction;
    }

    private static (int x, int y) Move(int x, int y, Dir d) => d switch
    {
        Dir.Right => (x + 1, y),
        Dir.Down  => (x, y + 1),
        Dir.Left  => (x - 1, y),
        Dir.Up    => (x, y - 1),
        _ => (x, y),
    };

    private static bool InBounds(int x, int y) => x >= 0 && x < COLS && y >= 0 && y < ROWS;

    // ── 클리어 체크 ────────────────────────────────────────────────────

    private void CheckClear()
    {
        if (_sandboxMode) return;
        var stage = _stages[_stageIndex];
        if (_collected < stage.GoalCount) return;

        _timer.Stop();
        _running = false;
        BtnPlay.Content = "▶ 시작";

        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var msg = $"🎉 스테이지 클리어!\n\n목표: {stage.GoalCount}개 수집\n경과: {elapsed:F1}초\n배치 기계: {CountMachines()}개";

        if (_stageIndex < _stages.Count - 1)
            msg += "\n\n▶ 버튼으로 다음 스테이지로 이동하세요!";
        else
            msg += "\n\n모든 스테이지 완료! 🏆\n샌드박스 모드에서 자유롭게 플레이하세요.";

        MessageBox.Show(msg, "클리어!", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private int CountMachines()
    {
        int count = 0;
        for (int x = 0; x < COLS; x++)
            for (int y = 0; y < ROWS; y++)
                if (!_grid[x, y].Locked && _grid[x, y].Type != MachineType.None)
                    count++;
        return count;
    }

    // ── UI 업데이트 ────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (!IsLoaded) return;

        // 스테이지 정보
        if (_sandboxMode)
        {
            TbStageName.Text = "샌드박스 모드";
            TbStageNum.Text  = "샌드박스";
            TbStageGoal.Text = "자유롭게 자동화 라인을 설계하세요!\n최고 처리량에 도전!";
            TbProgress.Text  = $"{_collected}";
            TbProgressLabel.Text = "수집";
            ProgressBar.Width = 0;
        }
        else
        {
            var stage = _stages[_stageIndex];
            TbStageName.Text  = stage.Name;
            TbStageNum.Text   = $"Stage {_stageIndex + 1} / {_stages.Count}  — {stage.Name}";
            TbStageGoal.Text  = stage.Description;
            TbProgress.Text   = $"{_collected} / {stage.GoalCount}";
            TbProgressLabel.Text = "수집됨";

            double maxW = 170.0;
            double ratio = Math.Min(1.0, (double)_collected / stage.GoalCount);
            ProgressBar.Width = maxW * ratio;
        }

        TbStageBtn.Text      = _sandboxMode ? "샌드박스" : $"Stage {_stageIndex + 1}";
        TbTick.Text          = $"틱 {_tick}";
        TbMachineCount.Text  = CountMachines().ToString();

        // 처리량 (아이템/분)
        double elapsedSec = _running ? (DateTime.Now - _startTime).TotalSeconds : 0;
        double perMin = elapsedSec > 1 ? _collected / elapsedSec * 60 : 0;
        TbThroughput.Text = $"{perMin:F1} /min";
        TbElapsed.Text    = elapsedSec > 0 ? $"{elapsedSec:F0}s" : "0s";

        // 속도
        string[] speedLabels = ["0.5×", "1×", "2×", "4×"];
        TbSpeed.Text = speedLabels[_speedIndex];
    }

    // ── 마우스 입력 ────────────────────────────────────────────────────

    private void Canvas_MouseLeft(object sender, MouseButtonEventArgs e)
    {
        PlaceAt(e.GetPosition(GameCanvas));
    }

    private void Canvas_MouseRight(object sender, MouseButtonEventArgs e)
    {
        DeleteAt(e.GetPosition(GameCanvas));
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            PlaceAt(e.GetPosition(GameCanvas));
        else if (e.RightButton == MouseButtonState.Pressed)
            DeleteAt(e.GetPosition(GameCanvas));
    }

    private void PlaceAt(Point p)
    {
        var (gx, gy) = ToGrid(p);
        if (!InBounds(gx, gy)) return;
        var cell = _grid[gx, gy];
        if (cell.Locked) return;
        if (_selectedTool == MachineType.None) return;

        cell.Type = _selectedTool;
        if (cell.Type == MachineType.Sorter)
            cell.FilterColor = _sorterFilter;

        DrawGrid();
        UpdateUI();
    }

    private void DeleteAt(Point p)
    {
        var (gx, gy) = ToGrid(p);
        if (!InBounds(gx, gy)) return;
        var cell = _grid[gx, gy];
        if (cell.Locked) return;
        cell.Type = MachineType.None;
        // 해당 셀 아이템 제거
        _items.RemoveAll(i => i.X == gx && i.Y == gy);
        DrawGrid();
        UpdateUI();
    }

    private static (int x, int y) ToGrid(Point p) =>
        ((int)(p.X / CELL), (int)(p.Y / CELL));

    // ── 키 입력 ───────────────────────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space: TogglePlay(); break;
            case Key.R:     ResetGame();  break;
            case Key.OemPlus:
            case Key.Add:   ChangeSpeed(+1); break;
            case Key.OemMinus:
            case Key.Subtract: ChangeSpeed(-1); break;
            // 분류기 색상 선택 (1~5)
            case Key.D1: _sorterFilter = ItemColor.Red;    break;
            case Key.D2: _sorterFilter = ItemColor.Green;  break;
            case Key.D3: _sorterFilter = ItemColor.Blue;   break;
            case Key.D4: _sorterFilter = ItemColor.Yellow; break;
            case Key.D5: _sorterFilter = ItemColor.Purple; break;
        }
    }

    // ── 버튼 핸들러 ───────────────────────────────────────────────────

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _selectedTool = (string?)btn.Tag switch
        {
            "BeltR" => MachineType.BeltR,
            "BeltD" => MachineType.BeltD,
            "BeltL" => MachineType.BeltL,
            "BeltU" => MachineType.BeltU,
            "Proc"  => MachineType.Processor,
            "Sort"  => MachineType.Sorter,
            "Merge" => MachineType.Merger,
            "Del"   => MachineType.None,
            _ => _selectedTool,
        };
        HighlightTool(btn);
    }

    private void HighlightTool(Button active)
    {
        var tools = new[] { BtnToolBeltR, BtnToolBeltD, BtnToolBeltL, BtnToolBeltU,
                            BtnToolProc, BtnToolSort, BtnToolMerge, BtnToolDel };
        foreach (var b in tools)
            b.Background = new SolidColorBrush(
                b == active
                    ? Color.FromRgb(0x20, 0x50, 0x20)
                    : Color.FromRgb(0x12, 0x22, 0x12));
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e) => TogglePlay();
    private void BtnReset_Click(object sender, RoutedEventArgs e) => ResetGame();
    private void BtnSpeedUp_Click(object sender, RoutedEventArgs e) => ChangeSpeed(+1);
    private void BtnSpeedDn_Click(object sender, RoutedEventArgs e) => ChangeSpeed(-1);

    private void BtnPrevStage_Click(object sender, RoutedEventArgs e)
    {
        if (_stageIndex <= 0) return;
        _sandboxMode = false;
        _stageIndex--;
        ResetGame();
    }

    private void BtnNextStage_Click(object sender, RoutedEventArgs e)
    {
        if (_stageIndex >= _stages.Count - 1) return;
        _sandboxMode = false;
        _stageIndex++;
        ResetGame();
    }

    private void BtnSandbox_Click(object sender, RoutedEventArgs e)
    {
        _sandboxMode = true;
        ResetGame();
    }

    // ── 게임 제어 ─────────────────────────────────────────────────────

    private void TogglePlay()
    {
        if (_running)
        {
            _timer.Stop();
            _running = false;
            BtnPlay.Content = "▶ 시작";
        }
        else
        {
            if (_tick == 0) _startTime = DateTime.Now;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_speeds[_speedIndex]),
            };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
            _running = true;
            BtnPlay.Content = "⏸ 정지";
        }
    }

    private void ResetGame()
    {
        _timer.Stop();
        _running = false;
        BtnPlay.Content = "▶ 시작";
        InitGrid();
        DrawGrid();
        UpdateUI();
    }

    private void ChangeSpeed(int delta)
    {
        _speedIndex = Math.Clamp(_speedIndex + delta, 0, _speeds.Length - 1);
        if (_running)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(_speeds[_speedIndex]);
        }
        UpdateUI();
    }
}
