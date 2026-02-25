using System.Windows.Forms;
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

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        Loaded += OnLoaded;
        SizeChanged += (_, _) => SkPreview.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadFonts();
        CbShape.SelectedIndex       = 0;
        CbOrientation.SelectedIndex = 2;
        CbFont.SelectedIndex        = 0;
        SliderMaxWords.Value        = 100;
        SliderMinFreq.Value         = 2;
        BuildThemeButtons();
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
        CbFont.SelectedIndex = 0;
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
            Background      = new SolidColorBrush(WpfColor.FromRgb(0x06, 0x20, 0x30)),
            BorderBrush     = new SolidColorBrush(WpfColor.FromRgb(0x06, 0xB6, 0xD4)),
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
    //  워드클라우드 생성
    // ─────────────────────────────────────────────────────────────
    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtInput.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            TxtStatus.Text = "텍스트를 입력하거나 파일을 불러오세요.";
            return;
        }

        BtnGenerate.IsEnabled       = false;
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

            int previewW = Math.Max(400, (int)SkPreview.ActualWidth);
            int previewH = Math.Max(300, (int)SkPreview.ActualHeight);

            var bitmap = await Task.Run(() =>
            {
                var freq = TextAnalysisService.Analyze(
                    text, cfg.MaxWords, cfg.MinFreq, _userStopWords);

                if (freq.Count == 0) return null;

                SKBitmap? mask = cfg.Shape != CloudShape.Rectangle
                    ? MaskService.Generate(cfg.Shape, previewW, previewH)
                    : null;

                return CloudGeneratorService.GenerateAsync(freq, cfg, mask).GetAwaiter().GetResult();
            });

            _currentBitmap?.Dispose();
            _currentBitmap = bitmap;
            SkPreview.InvalidateVisual();

            TxtStatus.Text = bitmap == null
                ? "단어가 부족합니다. 최소 빈도를 낮춰보세요."
                : "생성 완료";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"오류: {ex.Message}";
        }
        finally
        {
            BtnGenerate.IsEnabled       = false;
            PrgGenerate.IsIndeterminate = false;
            BtnGenerate.IsEnabled       = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  SKElement 렌더링
    // ─────────────────────────────────────────────────────────────
    private void SkPreview_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x1A, 0x1A, 0x28));

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
        try { ExportService.SaveJpeg(_currentBitmap!); }
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
}
