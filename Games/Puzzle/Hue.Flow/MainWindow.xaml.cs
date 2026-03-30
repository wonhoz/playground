using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HueFlow.Game;
using HueFlow.Sound;

namespace HueFlow;

public partial class MainWindow : Window
{
    // ── 색상 팔레트 (IconGenerator와 동일 순서) ──────────────────────
    // Catppuccin Mocha 기반 파스텔 6색 — 다크 배경에서 시각적으로 명확히 구별
    private static readonly string[] ColorHex =
    [
        "#F38BA8",   // 0 Red    (pastel coral-red)
        "#89B4FA",   // 1 Blue   (pastel periwinkle)
        "#A6E3A1",   // 2 Green  (pastel mint)
        "#FAB387",   // 3 Peach  (pastel orange)
        "#CBA6F7",   // 4 Mauve  (pastel purple)
        "#89DCEB",   // 5 Sky    (pastel teal-blue)
    ];

    private static readonly SolidColorBrush[] ColorBrushes =
        ColorHex.Select(h => {
            var b = new SolidColorBrush(ColorConverter.ConvertFromString(h) is Color c ? c : Colors.Transparent);
            b.Freeze();
            return b;
        }).ToArray();

    // ── 최고 점수 저장 경로 ──────────────────────────────────────────
    private static readonly string BestFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "HueFlow", "best.txt");

    // ── 게임 상태 ────────────────────────────────────────────────────
    private readonly FloodBoard _board = new();
    private readonly Border[,]  _tiles = new Border[FloodBoard.Size, FloodBoard.Size];
    private readonly Button[]   _colorBtns = new Button[FloodBoard.Colors];
    private int _bestMoves = int.MaxValue;

    // ── DWM 다크 타이틀바 ────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowIcon();
        LoadBest();
        InitTiles();
        InitColorButtons();
        UpdateUI();
        SoundGen.PlayBgm(Sounds.Bgm);
    }

    private void LoadWindowIcon()
    {
        try
        {
            var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"));
            if (sri != null) Icon = BitmapFrame.Create(sri.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch { }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    // ── 타일 초기화 ──────────────────────────────────────────────────
    private void InitTiles()
    {
        TileGrid.Children.Clear();
        for (int r = 0; r < FloodBoard.Size; r++)
            for (int c = 0; c < FloodBoard.Size; c++)
            {
                var border = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Margin       = new Thickness(1),
                };
                _tiles[r, c] = border;
                TileGrid.Children.Add(border);
            }
    }

    // ── 색상 버튼 초기화 ─────────────────────────────────────────────
    private void InitColorButtons()
    {
        ColorPanel.Children.Clear();
        for (int i = 0; i < FloodBoard.Colors; i++)
        {
            var btn = new Button
            {
                Background = ColorBrushes[i],
                Style      = (Style)Resources["ColorBtn"],
                Margin     = new Thickness(3),
                Tag        = i,
            };
            btn.Click += ColorBtn_Click;
            _colorBtns[i] = btn;
            ColorPanel.Children.Add(btn);
        }
    }

    // ── UI 갱신 ──────────────────────────────────────────────────────
    private void UpdateUI()
    {
        var grid = _board.Grid;

        // 타일 색상 갱신
        for (int r = 0; r < FloodBoard.Size; r++)
            for (int c = 0; c < FloodBoard.Size; c++)
                _tiles[r, c].Background = ColorBrushes[grid[r, c]];

        // 이동 수 & 최고 기록
        int moves      = _board.Moves;
        int maxMoves   = FloodBoard.MaxMoves;
        MovesText.Text = $"{moves} / {maxMoves}";
        BestText.Text  = _bestMoves == int.MaxValue ? "-" : _bestMoves.ToString();

        // 이동 수 색상 경고 (남은 이동 ≤ 5)
        int remaining = maxMoves - moves;
        MovesText.Foreground = remaining <= 5
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
            : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        // 진행도 바
        double pct = _board.TerritorySize / (double)(FloodBoard.Size * FloodBoard.Size);
        ProgressText.Text = $"{(int)(pct * 100)}%";

        // ProgressFill 너비는 트랙 너비에 비례 → ActualWidth 사용
        ProgressFill.Width = ProgressTrack.ActualWidth > 0
            ? ProgressTrack.ActualWidth * pct
            : 0;

        // 현재 영역 색과 같은 버튼 비활성화
        int currentColor = grid[0, 0];
        for (int i = 0; i < FloodBoard.Colors; i++)
            _colorBtns[i].IsEnabled = (i != currentColor) && !_board.IsWon && !_board.IsLost;

        // 게임 종료 판정
        if (_board.IsWon)
        {
            // 최고 기록 갱신
            if (moves < _bestMoves)
            {
                _bestMoves = moves;
                SaveBest();
                BestText.Text = _bestMoves.ToString();
            }
            ShowOverlay(won: true);
        }
        else if (_board.IsLost)
        {
            ShowOverlay(won: false);
        }
    }

    // ── 오버레이 표시 ────────────────────────────────────────────────
    private void ShowOverlay(bool won)
    {
        if (won)
        {
            OverlayEmoji.Text = "🎉";
            OverlayTitle.Text = "클리어!";
            OverlayMsg.Text   = $"{_board.Moves}회 만에 전체를 채웠습니다!\n" +
                                 (_board.Moves == _bestMoves ? "🏆 새 최고 기록!" : $"최고 기록: {_bestMoves}회");
            OverlayTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        }
        else
        {
            OverlayEmoji.Text = "💀";
            OverlayTitle.Text = "실패";
            int covered  = _board.TerritorySize;
            int total    = FloodBoard.Size * FloodBoard.Size;
            OverlayMsg.Text   = $"30회 안에 완성하지 못했습니다.\n{covered}/{total} 칸 완료";
            OverlayTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
        }
        Overlay.Visibility = Visibility.Visible;
        SoundGen.StopBgm();
        if (won) SoundGen.Sfx(Sounds.WinSfx);
        else     SoundGen.Sfx(Sounds.LoseSfx);
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────
    private void ColorBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int color)
        {
            SoundGen.Sfx(Sounds.ClickSfx);
            _board.ChooseColor(color);
            UpdateUI();
        }
    }

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        _board.Reset();
        Overlay.Visibility = Visibility.Collapsed;

        // 진행도 바 초기화 (레이아웃 전에 0으로)
        ProgressFill.Width = 0;

        UpdateUI();
        SoundGen.PlayBgm(Sounds.Bgm);
    }

    // ── 최고 기록 저장/불러오기 ─────────────────────────────────────
    private void LoadBest()
    {
        try
        {
            if (File.Exists(BestFilePath) &&
                int.TryParse(File.ReadAllText(BestFilePath).Trim(), out int v) && v > 0)
                _bestMoves = v;
        }
        catch { }
    }

    private void SaveBest()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BestFilePath)!);
            File.WriteAllText(BestFilePath, _bestMoves.ToString());
        }
        catch { }
    }

    // ── 레이아웃 변경 시 진행도 바 재계산 ───────────────────────────
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (ProgressTrack.ActualWidth > 0 && _board.TerritorySize > 0)
        {
            double pct = _board.TerritorySize / (double)(FloodBoard.Size * FloodBoard.Size);
            ProgressFill.Width = ProgressTrack.ActualWidth * pct;
        }
    }
}
