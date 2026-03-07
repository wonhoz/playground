using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Key.Map;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── 카테고리 색상 ──────────────────────────────────────────────────────────
    static readonly Dictionary<string, Color> CatColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["File"]     = Color.FromRgb(0x3B, 0x82, 0xF6),  // 파랑
        ["Edit"]     = Color.FromRgb(0xF5, 0x9E, 0x0B),  // 주황
        ["View"]     = Color.FromRgb(0x8B, 0x5C, 0xF6),  // 보라
        ["Run"]      = Color.FromRgb(0x10, 0xB9, 0x81),  // 초록
        ["Navigate"] = Color.FromRgb(0x06, 0xB6, 0xD4),  // 시안
        ["Other"]    = Color.FromRgb(0x6B, 0x72, 0x80),  // 회색
    };

    static readonly Color KeyBg      = Color.FromRgb(0x1E, 0x1E, 0x2E);
    static readonly Color KeyBorder  = Color.FromRgb(0x35, 0x35, 0x50);
    static readonly Color ModKeyBg   = Color.FromRgb(0x16, 0x16, 0x22);
    static readonly Color KeyFg      = Color.FromRgb(0xCC, 0xCC, 0xDD);

    static readonly HashSet<string> ModKeys = [
        "LControlKey","RControlKey","LShiftKey","RShiftKey",
        "LMenu","RMenu","LWin","RWin","Tab","CapsLock",
        "Back","Return","Apps","Space"
    ];

    readonly MainViewModel                  _vm;
    readonly Dictionary<string, Border>     _keyBorders = [];
    readonly HashSet<ShortcutEntry>         _highlighted = [];

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
            BuildKeyboard();
            BuildLegend();
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActivePreset))
                ClearHighlight();
        };
    }

    // ── 키보드 구축 ───────────────────────────────────────────────────────────

    void BuildKeyboard()
    {
        KbCanvas.Children.Clear();
        _keyBorders.Clear();

        double U = LayoutLibrary.U;

        foreach (var key in LayoutLibrary.AnsiKeys)
        {
            double x = key.X * U;
            double y = key.Y * U;
            double w = key.W * U - 2;
            double h = key.H * U - 2;

            bool isMod = ModKeys.Contains(key.Code);
            Color bg = isMod ? ModKeyBg : KeyBg;

            var border = new Border
            {
                Width       = w,
                Height      = h,
                CornerRadius = new CornerRadius(key.W > 1.5 ? 6 : 4),
                Background   = new SolidColorBrush(bg),
                BorderBrush  = new SolidColorBrush(KeyBorder),
                BorderThickness = new Thickness(1),
                Tag          = key,
                ToolTip      = key.Code
            };

            // ラベル
            var lbl = new TextBlock
            {
                Text              = key.Label,
                FontSize          = key.W >= 1.5 || key.Label.Length > 3 ? 9 : 10,
                FontFamily        = new FontFamily("Segoe UI"),
                Foreground        = new SolidColorBrush(KeyFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                TextWrapping        = TextWrapping.Wrap
            };
            border.Child = lbl;

            Canvas.SetLeft(border, x + 1);
            Canvas.SetTop(border, y + 1);
            KbCanvas.Children.Add(border);
            _keyBorders[key.Code] = border;
        }

        // Canvas 크기 설정
        KbCanvas.Width  = 15 * U + 2;
        KbCanvas.Height = (5.25 + 1.0) * U + 2;
    }

    // ── 범례 구축 ─────────────────────────────────────────────────────────────

    void BuildLegend()
    {
        LegendPanel.Children.Clear();
        foreach (var (cat, color) in CatColors)
        {
            var dot = new Border
            {
                Width = 10, Height = 10,
                CornerRadius = new CornerRadius(5),
                Background   = new SolidColorBrush(color),
                Margin       = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var txt = new TextBlock
            {
                Text      = cat,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88,0x88,0x99)),
                FontSize   = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin    = new Thickness(0, 0, 14, 0)
            };
            LegendPanel.Children.Add(dot);
            LegendPanel.Children.Add(txt);
        }
    }

    // ── 단축키 선택 → 키 하이라이팅 ─────────────────────────────────────────

    void Shortcut_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        bool multi = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (!multi) _highlighted.Clear();

        if (_vm.Selected != null) _highlighted.Add(_vm.Selected);

        ApplyHighlight();
    }

    void ApplyHighlight()
    {
        // 전체 리셋
        ClearHighlight(resetSet: false);

        foreach (var entry in _highlighted)
        {
            var keys  = LayoutLibrary.ParseShortcut(entry.Keys);
            var color = CatColors.TryGetValue(entry.Category, out var c) ? c : CatColors["Other"];

            foreach (var code in keys)
            {
                // Ctrl/Shift/Alt → 양쪽 모두 하이라이트
                string[] targets = code switch
                {
                    "LControlKey" or "RControlKey" => ["LControlKey","RControlKey"],
                    "LShiftKey"   or "RShiftKey"   => ["LShiftKey","RShiftKey"],
                    "LMenu"       or "RMenu"        => ["LMenu","RMenu"],
                    "LWin"        or "RWin"         => ["LWin","RWin"],
                    _ => [code]
                };

                foreach (var t in targets)
                {
                    if (_keyBorders.TryGetValue(t, out var b))
                    {
                        b.Background = new SolidColorBrush(
                            Color.FromArgb(0xFF, color.R, color.G, color.B));
                        if (b.Child is TextBlock tb)
                            tb.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
            }
        }
    }

    void ClearHighlight(bool resetSet = true)
    {
        if (resetSet) _highlighted.Clear();

        foreach (var (code, border) in _keyBorders)
        {
            bool isMod = ModKeys.Contains(code);
            border.Background = new SolidColorBrush(isMod ? ModKeyBg : KeyBg);
            if (border.Child is TextBlock tb)
                tb.Foreground = new SolidColorBrush(KeyFg);
        }
    }

    // ── 버튼 핸들러 ───────────────────────────────────────────────────────────

    void Add_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddShortcut();
        TxtNewKeys.Focus();
    }

    void Delete_Click(object sender, RoutedEventArgs e) => _vm.RemoveSelected();

    void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "PNG로 저장",
            Filter     = "PNG 이미지|*.png",
            FileName   = $"keymap_{_vm.ActivePreset?.Name ?? "custom"}_{DateTime.Now:yyyyMMdd}.png",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() != true) return;

        // 키보드 캔버스 렌더링
        var canvas = KbCanvas;
        canvas.Measure(new Size(canvas.Width, canvas.Height));
        canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));

        double dpi = 192;  // 2x 해상도
        var rtb = new RenderTargetBitmap(
            (int)(canvas.Width  * dpi / 96),
            (int)(canvas.Height * dpi / 96),
            dpi, dpi, PixelFormats.Pbgra32);

        // 배경 포함 렌더링
        var dv = new System.Windows.Media.DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x13,0x13,0x1F)),
                null, new Rect(0, 0, canvas.Width * dpi / 96, canvas.Height * dpi / 96));
        }
        rtb.Render(dv);

        var vb = new VisualBrush(canvas) { Stretch = Stretch.None };
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width  = canvas.Width  * dpi / 96,
            Height = canvas.Height * dpi / 96,
            Fill   = vb
        };
        rect.Measure(new Size(rect.Width, rect.Height));
        rect.Arrange(new Rect(0, 0, rect.Width, rect.Height));
        rtb.Render(rect);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = new FileStream(dlg.FileName, FileMode.Create);
        enc.Save(fs);

        _vm.StatusText = $"저장 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    void Print_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        // PrintVisual: 단순하게 캔버스를 직접 인쇄
        double scaleX = dlg.PrintableAreaWidth  / KbCanvas.Width;
        double scaleY = dlg.PrintableAreaHeight / KbCanvas.Height;
        double scale  = Math.Min(scaleX, scaleY) * 0.9;

        var transform = new ScaleTransform(scale, scale);
        KbCanvas.LayoutTransform = transform;
        KbCanvas.UpdateLayout();

        dlg.PrintVisual(KbCanvas, $"Key.Map — {_vm.ActivePreset?.Name}");

        KbCanvas.LayoutTransform = Transform.Identity;
        KbCanvas.UpdateLayout();

        _vm.StatusText = "인쇄 완료";
    }
}
