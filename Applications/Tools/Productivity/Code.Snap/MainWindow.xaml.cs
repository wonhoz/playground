using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.AvalonEdit;

namespace CodeSnap;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
    [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
    private static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    private readonly SnapConfig _cfg = new();
    private NotifyIcon? _tray;
    private HotkeyService? _hotkey;

    public MainWindow()
    {
        SetPreferredAppMode(2);
        FlushMenuThemes();

        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        Loaded += OnLoaded;
        Closing += (_, e) => { e.Cancel = true; Hide(); };

        AllowDrop = true;
        Drop += OnDrop;
        CodeEditor.TextChanged += OnCodeEditorChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetupTray();
        _hotkey = new HotkeyService(this, OnHotkey);
        // 초기 상태 적용
        CbLanguage.SelectedIndex = 0;
        CbFont.SelectedIndex = 0;
        CbFontSize.SelectedIndex = 3;   // 14
        CbGradient.SelectedIndex = 0;
        RefreshPreview();
    }

    // ────────────────────────────────────────────────────────────
    //  트레이 설정
    // ────────────────────────────────────────────────────────────
    private void SetupTray()
    {
        var iconPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
        Icon? icon = null;
        if (System.IO.File.Exists(iconPath))
            icon = new Icon(iconPath);

        _tray = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = "Code.Snap",
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f)
        };

        var open = new ToolStripMenuItem("Code.Snap 열기");
        open.Click += (_, _) => ShowWindow();
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("종료");
        exit.Click += (_, _) =>
        {
            _tray.Visible = false;
            _hotkey?.Dispose();
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exit);
        _tray.ContextMenuStrip = menu;
        _tray.ShowBalloonTip(2000, "Code.Snap", "실행 중 — Ctrl+Shift+C로 즉시 스냅", ToolTipIcon.Info);
    }

    private void ShowWindow()
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
    }

    // ────────────────────────────────────────────────────────────
    //  전역 단축키
    // ────────────────────────────────────────────────────────────
    private void OnHotkey()
    {
        var text = System.Windows.Clipboard.GetText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            CodeEditor.Document.Text = text;
            AutoDetectLanguage(text);
        }
        ShowWindow();
    }

    // ────────────────────────────────────────────────────────────
    //  파일 드래그 앤 드롭
    // ────────────────────────────────────────────────────────────
    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)!;
        var file = files.FirstOrDefault();
        if (file == null) return;

        try
        {
            var text = System.IO.File.ReadAllText(file);
            CodeEditor.Document.Text = text;
            var ext = System.IO.Path.GetExtension(file).TrimStart('.').ToLower();
            SetLanguageCombo(ExtToLang(ext));
            TxtFileName.Text = System.IO.Path.GetFileName(file);
        }
        catch { /* 읽기 실패 무시 */ }
    }

    private static string ExtToLang(string ext) => ext switch
    {
        "cs"            => "C#",
        "py"            => "Python",
        "js"            => "JavaScript",
        "ts"            => "TypeScript",
        "java"          => "Java",
        "html" or "htm" => "HTML",
        "css"           => "CSS",
        "sql"           => "SQL",
        "xml"           => "XML",
        "json"          => "JSON",
        "md"            => "Markdown",
        "php"           => "PHP",
        "rb"            => "Ruby",
        "go"            => "Go",
        "rs"            => "Rust",
        _               => "자동 감지"
    };

    // ────────────────────────────────────────────────────────────
    //  코드 편집기 이벤트
    // ────────────────────────────────────────────────────────────
    private void OnCodeEditorChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded) return;

        PreviewEditor.Document = CodeEditor.Document;

        if (CbLanguage.SelectedIndex == 0)
            AutoDetectLanguage(CodeEditor.Document.Text);
        else
            ApplySyntaxTheme();
    }

    private void AutoDetectLanguage(string code)
    {
        var lang = LanguageDetectService.Detect(code);
        _cfg.Language = lang;
        TxtLangStatus.Text = $"  ({lang})";
        ApplySyntaxTheme();
    }

    // ────────────────────────────────────────────────────────────
    //  옵션 패널 이벤트
    // ────────────────────────────────────────────────────────────
    private void CbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var item = (CbLanguage.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "자동 감지";
        if (item == "자동 감지")
            AutoDetectLanguage(CodeEditor.Document.Text);
        else
        {
            _cfg.Language = item;
            TxtLangStatus.Text = "";
            ApplySyntaxTheme();
        }
    }

    private void CbFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _cfg.FontFamily = (CbFont.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Cascadia Code";
        PreviewEditor.FontFamily = new System.Windows.Media.FontFamily(_cfg.FontFamily);
    }

    private void CbFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (int.TryParse((CbFontSize.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString(), out var sz))
        {
            _cfg.FontSize = sz;
            PreviewEditor.FontSize = sz;
        }
    }

    private void TxtFileName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _cfg.FileName = TxtFileName.Text;
        TbPreviewFile.Text = _cfg.FileName;
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _cfg.Theme = sender switch
        {
            System.Windows.Controls.RadioButton rb when rb == RbDarkPlus  => CodeTheme.DarkPlus,
            System.Windows.Controls.RadioButton rb when rb == RbGitHub    => CodeTheme.GitHub,
            System.Windows.Controls.RadioButton rb when rb == RbNord      => CodeTheme.Nord,
            System.Windows.Controls.RadioButton rb when rb == RbMonokai   => CodeTheme.Monokai,
            System.Windows.Controls.RadioButton rb when rb == RbSolarized => CodeTheme.Solarized,
            _                                                               => CodeTheme.Dracula,
        };
        ApplySyntaxTheme();
    }

    private void Bg_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (RbBgGradient.IsChecked == true)
        {
            _cfg.BgType = BackgroundType.Gradient;
            CbGradient.Visibility = Visibility.Visible;
            BtnSolidColor.Visibility = Visibility.Collapsed;
        }
        else if (RbBgSolid.IsChecked == true)
        {
            _cfg.BgType = BackgroundType.Solid;
            CbGradient.Visibility = Visibility.Collapsed;
            BtnSolidColor.Visibility = Visibility.Visible;
        }
        else
        {
            _cfg.BgType = BackgroundType.Transparent;
            CbGradient.Visibility = Visibility.Collapsed;
            BtnSolidColor.Visibility = Visibility.Collapsed;
        }
        RefreshBackground();
    }

    private void CbGradient_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _cfg.GradientIndex = CbGradient.SelectedIndex;
        RefreshBackground();
    }

    private void BtnSolidColor_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ColorDialog { Color = ColorTranslator.FromHtml(_cfg.SolidColor) };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _cfg.SolidColor = ColorTranslator.ToHtml(dlg.Color);
            RefreshBackground();
        }
    }

    private void SliderPadding_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _cfg.Padding = (int)SliderPadding.Value;
        PaddingBorder.Padding = new Thickness(_cfg.Padding);
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _cfg.RoundCorners    = ChkRoundCorners.IsChecked == true;
        _cfg.ShowShadow      = ChkShadow.IsChecked == true;
        _cfg.ShowWindowDeco  = ChkWindowDeco.IsChecked == true;
        _cfg.ShowLineNumbers = ChkLineNumbers.IsChecked == true;

        CodeCard.CornerRadius   = _cfg.RoundCorners ? new CornerRadius(8) : new CornerRadius(0);
        WindowDeco.CornerRadius = _cfg.RoundCorners ? new CornerRadius(8, 8, 0, 0) : new CornerRadius(0);
        CardShadow.Opacity      = _cfg.ShowShadow ? 0.5 : 0.0;
        WindowDeco.Visibility   = _cfg.ShowWindowDeco ? Visibility.Visible : Visibility.Collapsed;
        DecoRow.Height          = _cfg.ShowWindowDeco ? new GridLength(36) : new GridLength(0);
        PreviewEditor.ShowLineNumbers = _cfg.ShowLineNumbers;
    }

    // ────────────────────────────────────────────────────────────
    //  내보내기
    // ────────────────────────────────────────────────────────────
    private void BtnSavePng_Click(object sender, RoutedEventArgs e) =>
        ExportService.SavePng(PreviewCanvas);

    private void BtnCopy_Click(object sender, RoutedEventArgs e) =>
        ExportService.CopyToClipboard(PreviewCanvas);

    // ────────────────────────────────────────────────────────────
    //  미리보기 갱신
    // ────────────────────────────────────────────────────────────
    private void RefreshPreview()
    {
        ApplySyntaxTheme();
        RefreshBackground();
    }

    private void ApplySyntaxTheme()
    {
        if (!IsLoaded) return;
        SyntaxThemeService.Apply(PreviewEditor, _cfg.Language, _cfg.Theme);
        var bgColor = GetThemeBgColor(_cfg.Theme);
        CodeCard.Background = new SolidColorBrush(bgColor);
        PreviewEditor.Background = new SolidColorBrush(bgColor);
    }

    private void RefreshBackground()
    {
        if (!IsLoaded) return;

        switch (_cfg.BgType)
        {
            case BackgroundType.Gradient:
                var preset = BackgroundPreset.Gradients[_cfg.GradientIndex];
                var c1 = ColorFromHex(preset.Color1);
                var c2 = ColorFromHex(preset.Color2);
                Gs1.Color = c1;
                Gs2.Color = c2;
                BgLayer.Background = new LinearGradientBrush(c1, c2, 45);
                BgLayer.Visibility = Visibility.Visible;
                break;

            case BackgroundType.Solid:
                BgLayer.Background = new SolidColorBrush(ColorFromHex(_cfg.SolidColor));
                BgLayer.Visibility = Visibility.Visible;
                break;

            case BackgroundType.Transparent:
                BgLayer.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private static System.Windows.Media.Color GetThemeBgColor(CodeTheme theme) => theme switch
    {
        CodeTheme.DarkPlus  => ColorFromHex("#1E1E1E"),
        CodeTheme.GitHub    => ColorFromHex("#FFFFFF"),
        CodeTheme.Nord      => ColorFromHex("#2E3440"),
        CodeTheme.Monokai   => ColorFromHex("#272822"),
        CodeTheme.Solarized => ColorFromHex("#002B36"),
        _                   => ColorFromHex("#282A36"),
    };

    private static System.Windows.Media.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return System.Windows.Media.Color.FromRgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private void SetLanguageCombo(string lang)
    {
        foreach (System.Windows.Controls.ComboBoxItem item in CbLanguage.Items)
        {
            if (item.Content?.ToString() == lang)
            {
                CbLanguage.SelectedItem = item;
                return;
            }
        }
        CbLanguage.SelectedIndex = 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkey?.Dispose();
        _tray?.Dispose();
        base.OnClosed(e);
    }
}

// ────────────────────────────────────────────────────────────
//  다크 메뉴 렌더러 (ToolStripRenderer 직접 상속)
// ────────────────────────────────────────────────────────────
internal sealed class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly System.Drawing.Color BgColor  = System.Drawing.Color.FromArgb(0x16, 0x16, 0x22);
    private static readonly System.Drawing.Color FgColor  = System.Drawing.Color.FromArgb(0xE0, 0xE0, 0xE0);
    private static readonly System.Drawing.Color HoverBg  = System.Drawing.Color.FromArgb(0x2A, 0x1A, 0x00);
    private static readonly System.Drawing.Color SepColor = System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3A);
    private static readonly System.Drawing.Color BorderClr= System.Drawing.Color.FromArgb(0x2A, 0x2A, 0x3A);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var p = new System.Drawing.Pen(BorderClr);
        var r = e.AffectedBounds;
        r.Width--; r.Height--;
        e.Graphics.DrawRectangle(p, r);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var b = new SolidBrush(e.Item.Selected ? HoverBg : BgColor);
        e.Graphics.FillRectangle(b, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? FgColor : System.Drawing.Color.FromArgb(0x50, 0x50, 0x68);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var p = new System.Drawing.Pen(SepColor);
        e.Graphics.DrawLine(p, 0, y, e.Item.Width, y);
    }
}
