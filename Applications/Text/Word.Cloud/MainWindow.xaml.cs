using System.Windows.Forms;
using System.Text.Json;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;

namespace WordCloud;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private readonly CloudConfig _config = new();
    private readonly HashSet<string> _userStopWords = [];
    private SKBitmap? _currentBitmap;
    private Sdcb.WordClouds.WordCloud? _currentWordCloud;
    private CancellationTokenSource? _cts;
    private (int w, int h) _exportResolution = (-1, -1);

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WordCloud", "settings.json");

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        Loaded   += OnLoaded;
        Closed   += (_, _) => SaveSettings();
        SizeChanged += (_, _) => SkPreview.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadFonts();
        BuildThemeButtons();
        LoadSettings();
        UpdateBgColorDisplay();
    }

    // ─────────────────────────────────────────────────────────────
    //  폰트 로드
    // ─────────────────────────────────────────────────────────────
    private void LoadFonts()
    {
        var fonts = new[]
        {
            "맑은 고딕", "나눔고딕", "Arial", "Consolas",
            "Georgia", "Impact", "Tahoma", "Verdana", "Comic Sans MS",
        };
        foreach (var f in fonts)
            CbFont.Items.Add(f);
    }

    // ─────────────────────────────────────────────────────────────
    //  테마 버튼 생성
    // ─────────────────────────────────────────────────────────────
    private void BuildThemeButtons()
    {
        PanelThemes.Children.Clear();
        for (int i = 0; i < ColorTheme.Names.Length; i++)
        {
            int idx = i;
            var btn = new System.Windows.Controls.RadioButton
            {
                Content   = ColorTheme.Names[i],
                Margin    = new Thickness(0, 0, 6, 6),
                IsChecked = (i == 0),
                GroupName = "Theme",
                FontSize  = 11,
            };
            btn.Checked += (_, _) =>
            {
                if (!IsLoaded) return;
                _config.ThemeIndex = idx;
            };
            PanelThemes.Children.Add(btn);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  설정 저장/복원
    // ─────────────────────────────────────────────────────────────
    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var s = new AppSettings
            {
                ShapeIndex       = CbShape.SelectedIndex,
                OrientationIndex = CbOrientation.SelectedIndex,
                FontName         = _config.FontName,
                MaxWords         = _config.MaxWords,
                MinFreq          = _config.MinFreq,
                ThemeIndex       = _config.ThemeIndex,
                BgColorHex       = $"#{_config.BgColor.Red:X2}{_config.BgColor.Green:X2}{_config.BgColor.Blue:X2}",
                ResolutionIndex  = CbResolution.SelectedIndex,
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));
        }
        catch { }
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                ApplyDefaultSettings();
                return;
            }
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (s == null) { ApplyDefaultSettings(); return; }

            CbShape.SelectedIndex       = Math.Clamp(s.ShapeIndex, 0, 6);
            CbOrientation.SelectedIndex = Math.Clamp(s.OrientationIndex, 0, 3);
            CbResolution.SelectedIndex  = Math.Clamp(s.ResolutionIndex, 0, 3);
            SliderMaxWords.Value        = Math.Clamp(s.MaxWords, 10, 500);
            SliderMinFreq.Value         = Math.Clamp(s.MinFreq, 1, 20);

            var fontIdx = CbFont.Items.IndexOf(s.FontName);
            if (fontIdx >= 0)
                CbFont.SelectedIndex = fontIdx;
            else
            {
                CbFont.Text = s.FontName;
                _config.FontName = s.FontName;
            }

            _config.ThemeIndex = Math.Clamp(s.ThemeIndex, 0, ColorTheme.Names.Length - 1);
            if (PanelThemes.Children[_config.ThemeIndex] is System.Windows.Controls.RadioButton rb)
                rb.IsChecked = true;

            if (TryParseHexColor(s.BgColorHex, out var bgColor))
                _config.BgColor = bgColor;
        }
        catch
        {
            ApplyDefaultSettings();
        }
    }

    private void ApplyDefaultSettings()
    {
        CbShape.SelectedIndex       = 0;
        CbOrientation.SelectedIndex = 2;
        CbFont.SelectedIndex        = 0;
        CbResolution.SelectedIndex  = 3;
        SliderMaxWords.Value        = 100;
        SliderMinFreq.Value         = 2;
    }

    private static bool TryParseHexColor(string? hex, out SKColor color)
    {
        color = new SKColor(0x0D, 0x0D, 0x16);
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return false;
        if (!byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r)) return false;
        if (!byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)) return false;
        if (!byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b)) return false;
        color = new SKColor(r, g, b);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    //  컨트롤 이벤트
    // ─────────────────────────────────────────────────────────────
    private void CbShape_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _config.Shape = CbShape.SelectedIndex switch
        {
            0 => CloudShape.Circle,
            1 => CloudShape.Rectangle,
            2 => CloudShape.Heart,
            3 => CloudShape.Star,
            4 => CloudShape.Diamond,
            5 => CloudShape.Cloud,
            _ => CloudShape.Random,
        };
    }

    private void CbOrientation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _config.Orientation = CbOrientation.SelectedIndex switch
        {
            0 => Models.TextOrientation.Horizontal,
            1 => Models.TextOrientation.Vertical,
            2 => Models.TextOrientation.Mixed,
            _ => Models.TextOrientation.Random,
        };
    }

    private void CbFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CbFont.SelectedItem is string fontName)
            _config.FontName = fontName;
    }

    private void CbFont_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var text = CbFont.Text.Trim();
        if (!string.IsNullOrEmpty(text))
            _config.FontName = text;
    }

    private void CbResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _exportResolution = CbResolution.SelectedIndex switch
        {
            0 => (1920, 1080),
            1 => (2560, 1600),
            2 => (3840, 2160),
            _ => (-1, -1),
        };
    }

    private void SliderMaxWords_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        var val = (int)SliderMaxWords.Value;
        _config.MaxWords = val;
        TxtMaxWords.Text = val.ToString();
    }

    private void SliderMinFreq_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        var val = (int)SliderMinFreq.Value;
        _config.MinFreq = val;
        TxtMinFreq.Text = val.ToString();
    }

    // ─────────────────────────────────────────────────────────────
    //  파일 불러오기 / 지우기
    // ─────────────────────────────────────────────────────────────
    private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "텍스트 파일 열기",
            Filter = "텍스트 파일|*.txt;*.md;*.csv|모든 파일|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            TxtInput.Text = File.ReadAllText(dlg.FileName);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"파일 읽기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e) => TxtInput.Clear();

    // ─────────────────────────────────────────────────────────────
    //  단어 빈도 미리보기
    // ─────────────────────────────────────────────────────────────
    private void BtnPreviewWords_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtInput.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            TxtStatus.Text = "텍스트를 먼저 입력하세요.";
            return;
        }

        var freq = TextAnalysisService.Analyze(text, 200, _config.MinFreq, _userStopWords);
        var items = freq.OrderByDescending(kv => kv.Value)
                        .Take(10)
                        .Select(kv => new WordFreqItem(kv.Key, kv.Value))
                        .ToList();

        LstFreqPreview.ItemsSource = items;
        TxtStatus.Text = items.Count == 0
            ? "표시할 단어가 없습니다. 최소 빈도를 낮춰보세요."
            : $"상위 {items.Count}개 단어";
    }

    // ─────────────────────────────────────────────────────────────
    //  불용어 관리
    // ─────────────────────────────────────────────────────────────
    private void TxtStopWord_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddStopWord();
    }

    private void BtnAddStop_Click(object sender, RoutedEventArgs e) => AddStopWord();

    private void AddStopWord()
    {
        var word = TxtStopWord.Text.Trim();
        if (string.IsNullOrEmpty(word)) return;
        if (_userStopWords.Add(word.ToLowerInvariant()))
            AddStopWordChip(word);
        TxtStopWord.Clear();
    }

    private void AddStopWordChip(string word)
    {
        var chip = new Border
        {
            Background      = new SolidColorBrush(WpfColor.FromRgb(0x2D, 0x0A, 0x18)),
            BorderBrush     = new SolidColorBrush(WpfColor.FromRgb(0xE9, 0x1E, 0x63)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(10),
            Margin          = new Thickness(0, 0, 4, 4),
            Padding         = new Thickness(6, 2, 6, 2),
            Cursor          = WpfCursors.Hand,
        };
        var tb = new TextBlock
        {
            Text       = $"✕ {word}",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0)),
            FontSize   = 11,
        };
        chip.Child = tb;
        chip.MouseLeftButtonUp += (_, _) =>
        {
            _userStopWords.Remove(word.ToLowerInvariant());
            PanelStopWords.Children.Remove(chip);
        };
        PanelStopWords.Children.Add(chip);
    }

    // ─────────────────────────────────────────────────────────────
    //  배경색 선택
    // ─────────────────────────────────────────────────────────────
    private void BdBgColor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => ShowColorDialog();
    private void BtnBgColor_Click(object sender, RoutedEventArgs e) => ShowColorDialog();

    private void ShowColorDialog()
    {
        var c = _config.BgColor;
        using var dlg = new ColorDialog
        {
            Color    = System.Drawing.Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue),
            FullOpen = true,
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var sc = dlg.Color;
        _config.BgColor = new SKColor(sc.R, sc.G, sc.B);
        UpdateBgColorDisplay();
    }

    private void UpdateBgColorDisplay()
    {
        var c = _config.BgColor;
        BdBgColor.Background = new SolidColorBrush(WpfColor.FromRgb(c.Red, c.Green, c.Blue));
    }

    // ─────────────────────────────────────────────────────────────
    //  워드클라우드 생성 / 취소
    // ─────────────────────────────────────────────────────────────
    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtInput.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            TxtStatus.Text = "텍스트를 입력하거나 파일을 불러오세요.";
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        BtnGenerate.IsEnabled       = false;
        BtnCancel.Visibility        = Visibility.Visible;
        PrgGenerate.IsIndeterminate = true;
        TxtStatus.Text = "분석 중...";

        try
        {
            var cfg = new CloudConfig
            {
                MaxWords    = _config.MaxWords,
                MinFreq     = _config.MinFreq,
                Shape       = _config.Shape,
                Orientation = _config.Orientation,
                FontName    = _config.FontName,
                ThemeIndex  = _config.ThemeIndex,
                BgColor     = _config.BgColor,
            };

            var (exportW, exportH) = GetExportSize();

            var freq = await Task.Run(() =>
                TextAnalysisService.Analyze(text, cfg.MaxWords, cfg.MinFreq, _userStopWords), token);

            token.ThrowIfCancellationRequested();

            if (freq.Count == 0)
            {
                TxtStatus.Text = "단어가 부족합니다. 최소 빈도를 낮춰보세요.";
                return;
            }

            TxtStatus.Text = "생성 중...";
            var (bitmap, wc) = await CloudGeneratorService.GenerateAsync(freq, cfg, exportW, exportH);

            token.ThrowIfCancellationRequested();

            _currentBitmap?.Dispose();
            _currentBitmap    = bitmap;
            _currentWordCloud = wc;
            SkPreview.InvalidateVisual();

            TxtStatus.Text = $"생성 완료 ({exportW}×{exportH})";
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text = "생성이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"오류: {ex.Message}";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            PrgGenerate.IsIndeterminate = false;
            BtnGenerate.IsEnabled       = true;
            BtnCancel.Visibility        = Visibility.Collapsed;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private (int w, int h) GetExportSize()
        => _exportResolution is (-1, -1)
            ? CloudGeneratorService.GetExportSize()
            : _exportResolution;

    // ─────────────────────────────────────────────────────────────
    //  SKElement 렌더링 (미리보기)
    // ─────────────────────────────────────────────────────────────
    private void SkPreview_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(_config.BgColor);

        if (_currentBitmap == null || _currentBitmap.IsNull) return;

        int surfW = e.Info.Width, surfH = e.Info.Height;
        float scale = Math.Min((float)surfW / _currentBitmap.Width,
                               (float)surfH / _currentBitmap.Height);
        float drawW = _currentBitmap.Width  * scale;
        float drawH = _currentBitmap.Height * scale;
        float ox = (surfW - drawW) / 2f;
        float oy = (surfH - drawH) / 2f;

        canvas.DrawBitmap(_currentBitmap, new SKRect(ox, oy, ox + drawW, oy + drawH));
    }

    // ─────────────────────────────────────────────────────────────
    //  내보내기
    // ─────────────────────────────────────────────────────────────
    private void BtnSavePng_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckBitmap()) return;
        try { ExportService.SavePng(_currentBitmap!); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnSaveJpeg_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckBitmap()) return;
        try { ExportService.SaveJpeg(_currentBitmap!, _config.BgColor); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnSaveSvg_Click(object sender, RoutedEventArgs e)
    {
        if (_currentWordCloud == null)
        {
            System.Windows.MessageBox.Show("먼저 워드클라우드를 생성하세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { ExportService.SaveSvg(_currentWordCloud); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void BtnClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckBitmap()) return;
        try
        {
            ExportService.CopyToClipboard(_currentBitmap!);
            TxtStatus.Text = "클립보드에 복사되었습니다.";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private bool CheckBitmap()
    {
        if (_currentBitmap == null || _currentBitmap.IsNull)
        {
            System.Windows.MessageBox.Show("먼저 워드클라우드를 생성하세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        return true;
    }

    private void ShowError(Exception ex)
        => System.Windows.MessageBox.Show($"오류:\n{ex.Message}", "오류",
               MessageBoxButton.OK, MessageBoxImage.Error);

    // ─────────────────────────────────────────────────────────────
    //  단축키
    // ─────────────────────────────────────────────────────────────
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var ctrl = (e.KeyboardDevice.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;

        switch (e.Key)
        {
            case Key.F1:
                ShowHelp();
                e.Handled = true;
                break;
            case Key.F5:
                if (BtnGenerate.IsEnabled)
                    BtnGenerate_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Escape:
                _cts?.Cancel();
                e.Handled = true;
                break;
            case Key.O when ctrl:
                BtnLoadFile_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S when ctrl:
                BtnSavePng_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e) => ShowHelp();

    private void ShowHelp()
    {
        var help = new HelpWindow { Owner = this };
        help.ShowDialog();
    }
}

// ─────────────────────────────────────────────────────────────
//  설정 모델
// ─────────────────────────────────────────────────────────────
public class AppSettings
{
    public int    ShapeIndex       { get; set; } = 0;
    public int    OrientationIndex { get; set; } = 2;
    public string FontName         { get; set; } = "맑은 고딕";
    public int    MaxWords         { get; set; } = 100;
    public int    MinFreq          { get; set; } = 2;
    public int    ThemeIndex       { get; set; } = 0;
    public string BgColorHex       { get; set; } = "#0D0D16";
    public int    ResolutionIndex  { get; set; } = 3;
}

// ─────────────────────────────────────────────────────────────
//  단어 빈도 아이템
// ─────────────────────────────────────────────────────────────
public record WordFreqItem(string Word, int Count);
