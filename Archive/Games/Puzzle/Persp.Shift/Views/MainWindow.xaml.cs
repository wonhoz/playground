using System.Runtime.InteropServices;
using System.Windows.Interop;
using PerspShift.Services;

namespace PerspShift.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int val, int sz);

    private readonly GameService _game = new();
    private int _levelIndex = 0;

    private readonly GridCanvas _frontCanvas;
    private readonly GridCanvas _topCanvas;
    private readonly GridCanvas _sideCanvas;

    public MainWindow()
    {
        InitializeComponent();

        _frontCanvas = new GridCanvas();
        _topCanvas   = new GridCanvas();
        _sideCanvas  = new GridCanvas();

        _frontCanvas.CellClicked = (x, y) => { _game.ToggleFront(x, y); Refresh(); };
        _topCanvas.CellClicked   = (x, z) => { _game.ToggleTop(x, z);   Refresh(); };
        _sideCanvas.CellClicked  = (y, z) => { _game.ToggleSide(y, z);  Refresh(); };

        ViewGrid.Children.Add(MakePanel("FRONT (앞)", _frontCanvas, 0));
        ViewGrid.Children.Add(MakePanel("TOP (위)",   _topCanvas,   1));
        ViewGrid.Children.Add(MakePanel("SIDE (옆)",  _sideCanvas,  2));

        Loaded += (_, _) => { ApplyDarkTitleBar(); LoadLevel(); };
    }

    private static StackPanel MakePanel(string label, GridCanvas canvas, int col)
    {
        var lbl = new TextBlock
        {
            Text                = label,
            FontSize            = 12,
            Foreground          = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 0, 0, 8),
        };
        var border = new Border
        {
            BorderBrush     = new SolidColorBrush(WpfColor.FromRgb(0x2A, 0x4A, 0x4A)),
            BorderThickness = new Thickness(2),
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(4),
            Child           = canvas,
        };
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(lbl);
        panel.Children.Add(border);
        Grid.SetColumn(panel, col);
        return panel;
    }

    private void LoadLevel()
    {
        var levels = LevelData.All;
        _levelIndex = Math.Clamp(_levelIndex, 0, levels.Count - 1);

        var lv = levels[_levelIndex];
        _game.LoadLevel(lv);

        TxtLevel.Text      = $"{lv.Number} / {levels.Count}";
        TxtLevelTitle.Text = $"— {lv.Title}";
        TxtStatus.Text     = "각 뷰에서 셀을 클릭하면 해당 줄 전체를 채우거나 지웁니다.  세 시점 실루엣을 목표(청록 테두리)에 맞추세요!";

        Refresh();
    }

    private void Refresh()
    {
        var lv = _game.CurrentLevel!;
        _frontCanvas.Update(_game.GetFrontSilhouette(), lv.FrontTarget);
        _topCanvas.Update  (_game.GetTopSilhouette(),   lv.TopTarget);
        _sideCanvas.Update (_game.GetSideSilhouette(),  lv.SideTarget);

        if (_game.IsSolved())
            TxtStatus.Text = "✅ 클리어!  다음 레벨로 진행하세요 ▶";
    }

    private void OnPrev(object sender, RoutedEventArgs e)  { _levelIndex--; LoadLevel(); }
    private void OnNext(object sender, RoutedEventArgs e)  { _levelIndex++; LoadLevel(); }
    private void OnReset(object sender, RoutedEventArgs e) { _game.Reset(); Refresh(); TxtStatus.Text = "초기화했습니다."; }

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }
}
