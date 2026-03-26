using System.Text.Json;
using System.Windows.Media.Animation;

namespace CharArt;

public partial class MainWindow : Window
{
    // ── DWM 다크 타이틀바 ────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── 상태 ────────────────────────────────────────────────────────
    private BitmapSource? _source;
    private string        _currentArt = "";
    private char[]        _orderedChars = [];

    // 디바운스
    private CancellationTokenSource _debounceCts = new();

    // 뷰어 줌 (생성 설정과 독립적인 디스플레이 크기)
    private double _viewFontSize = 8.0;
    private const double ViewFontMin = 4.0;
    private const double ViewFontMax = 24.0;

    // 설정
    private AppSettings _settings = new();

    // 폰트 목록
    private static readonly string[] Fonts = ["Consolas", "Courier New", "굴림체", "맑은 고딕", "D2Coding"];

    // 폰트 크기 목록
    private static readonly double[] FontSizes = [6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0];

    // ── 생성자 ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // 설정 로드
        _settings = AppSettings.Load();

        // ComboBox 초기화 (Loaded 이후이므로 이벤트 안전)
        CbCharSet.ItemsSource  = CharSetLibrary.PresetNames;
        CbFont.ItemsSource     = Fonts;
        CbFontSize.ItemsSource = FontSizes.Select(f => $"{f}pt").ToList();

        // 저장된 설정 복원
        var charSetIdx = Array.IndexOf(CharSetLibrary.PresetNames, _settings.CharSetName);
        CbCharSet.SelectedIndex = charSetIdx >= 0 ? charSetIdx : 0;

        var fontIdx = Array.IndexOf(Fonts, _settings.FontFamily);
        CbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;

        CbFontSize.SelectedIndex = Math.Clamp(_settings.FontSizeIndex, 0, FontSizes.Length - 1);

        SldCols.Value      = Math.Clamp(_settings.Columns, 20, 300);
        ChkInvert.IsChecked = _settings.Invert;

        UpdateColsLabel();
        RefreshRecentMenu();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SaveSettings();
        _debounceCts.Cancel();
        _debounceCts.Dispose();
    }

    private void SaveSettings()
    {
        _settings.CharSetName   = CbCharSet.SelectedItem as string ?? "ASCII 기본";
        _settings.FontFamily    = CbFont.SelectedItem as string ?? "Consolas";
        _settings.FontSizeIndex = CbFontSize.SelectedIndex;
        _settings.Columns       = (int)SldCols.Value;
        _settings.Invert        = ChkInvert.IsChecked == true;
        _settings.Save();
    }

    // ── 이벤트: 이미지 열기 ──────────────────────────────────────────
    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "이미지 열기",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|모든 파일|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        LoadImage(dlg.FileName);
    }

    private void BtnRecentDrop_Click(object sender, RoutedEventArgs e)
    {
        if (BtnRecentDrop.ContextMenu is { } menu)
        {
            menu.PlacementTarget = BtnRecentDrop;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void LoadImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            _source = bmp;

            ImgPreview.Source      = bmp;
            ImgPreview.Visibility  = Visibility.Visible;
            TxtDropHint.Visibility = Visibility.Collapsed;

            BtnGenerate.IsEnabled = true;
            SetStatus($"이미지 로드: {Path.GetFileName(path)}  ({bmp.PixelWidth}×{bmp.PixelHeight})");

            // 최근 파일 저장
            _settings.AddRecentFile(path);
            RefreshRecentMenu();

            ScheduleGenerate();
        }
        catch (Exception ex)
        {
            SetStatus($"이미지 로드 실패: {ex.Message}");
        }
    }

    private void RefreshRecentMenu()
    {
        if (BtnRecentDrop.ContextMenu is not { } menu) return;
        menu.Items.Clear();

        if (_settings.RecentFiles.Count == 0)
        {
            var empty = new MenuItem
            {
                Header     = "최근 파일 없음",
                IsEnabled  = false,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#88A8A8")),
            };
            menu.Items.Add(empty);
            return;
        }

        foreach (var path in _settings.RecentFiles)
        {
            var item = new MenuItem
            {
                Header     = Path.GetFileName(path),
                ToolTip    = path,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8E8E0")),
            };
            var capturedPath = path;
            item.Click += (_, _) => LoadImage(capturedPath);
            menu.Items.Add(item);
        }
    }

    // ── 이벤트: 드래그앤드롭 ────────────────────────────────────────
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 0) return;
        LoadImage(files[0]);
    }

    // ── 이벤트: 키보드 단축키 ────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl  = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (e.Key == Key.F1) { BtnHelp_Click(sender, e); return; }
        if (e.Key == Key.F5 && BtnGenerate.IsEnabled) { BtnGenerate_Click(sender, e); return; }

        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.O:
                    BtnOpen_Click(sender, e);
                    e.Handled = true; break;
                case Key.S when !shift && BtnSave.IsEnabled:
                    BtnSave_Click(sender, e);
                    e.Handled = true; break;
                case Key.S when shift && BtnSavePng.IsEnabled:
                    BtnSavePng_Click(sender, e);
                    e.Handled = true; break;
                case Key.H when shift && BtnSaveHtml.IsEnabled:
                    BtnSaveHtml_Click(sender, e);
                    e.Handled = true; break;
                case Key.C when BtnCopy.IsEnabled:
                    if (!TxtArt.IsFocused) { BtnCopy_Click(sender, e); e.Handled = true; }
                    break;
                case Key.D0:
                case Key.NumPad0:
                    ResetViewZoom();
                    e.Handled = true; break;
            }
        }
    }

    // ── 이벤트: Ctrl+마우스휠 줌 ────────────────────────────────────
    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        _viewFontSize = e.Delta > 0
            ? Math.Min(_viewFontSize + 1, ViewFontMax)
            : Math.Max(_viewFontSize - 1, ViewFontMin);

        TxtArt.FontSize = _viewFontSize;
        UpdateZoomInfo();
        e.Handled = true;
    }

    private void ResetViewZoom()
    {
        _viewFontSize = CbFontSize.SelectedIndex >= 0 && CbFontSize.SelectedIndex < FontSizes.Length
            ? FontSizes[CbFontSize.SelectedIndex]
            : 8.0;
        TxtArt.FontSize = _viewFontSize;
        TxtZoomInfo.Visibility = Visibility.Collapsed;
    }

    private void UpdateZoomInfo()
    {
        TxtZoomInfo.Text       = $"뷰 {_viewFontSize:F0}pt  (Ctrl+0 초기화)";
        TxtZoomInfo.Visibility = Visibility.Visible;
    }

    // ── 이벤트: 설정 변경 ────────────────────────────────────────────
    private void Setting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var charSetName = CbCharSet.SelectedItem as string ?? "";
        if (CharSetLibrary.IsFullWidth(charSetName))
        {
            int gulimIdx = Array.IndexOf(Fonts, "굴림체");
            if (gulimIdx >= 0 && CbFont.SelectedIndex != gulimIdx)
            {
                CbFont.SelectedIndex = gulimIdx;
                return;
            }
        }

        PanelCustom.Visibility = charSetName == "커스텀"
            ? Visibility.Visible
            : Visibility.Collapsed;

        ScheduleGenerate();
    }

    private void Setting_Changed_Text(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ScheduleGenerate();
    }

    private void Setting_Changed_Check(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ScheduleGenerate();
    }

    private void SldCols_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        UpdateColsLabel();
        ScheduleGenerate();
    }

    private void PresetCols_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag as string, out int cols))
            SldCols.Value = cols;
    }

    private void UpdateColsLabel()
    {
        TxtColsValue.Text = ((int)SldCols.Value).ToString();
    }

    // ── 이벤트: 저장 / 복사 ─────────────────────────────────────────
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentArt)) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "텍스트 아트 저장",
            Filter     = "텍스트 파일|*.txt|모든 파일|*.*",
            DefaultExt = ".txt",
            FileName   = "char-art",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, _currentArt, new System.Text.UTF8Encoding(true));
            SetStatus($"저장 완료: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($"저장 실패: {ex.Message}");
        }
    }

    private void BtnSavePng_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentArt)) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "PNG로 저장",
            Filter     = "PNG 이미지|*.png",
            DefaultExt = ".png",
            FileName   = "char-art",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SetStatus("PNG 렌더링 중...");
            var config = BuildConfig();
            SaveAsPng(dlg.FileName, config);
            SetStatus($"PNG 저장 완료: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($"PNG 저장 실패: {ex.Message}");
        }
    }

    private void SaveAsPng(string filePath, ArtConfig config)
    {
        var tb = new TextBlock
        {
            Text       = _currentArt,
            FontFamily = new FontFamily(config.FontFamily),
            FontSize   = config.FontSize,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8E8E0")),
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1A1E")),
            Padding    = new Thickness(12),
            TextWrapping = TextWrapping.NoWrap,
        };

        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        tb.Arrange(new Rect(tb.DesiredSize));

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(tb.ActualWidth),
            (int)Math.Ceiling(tb.ActualHeight),
            96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(tb);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(filePath);
        encoder.Save(fs);
    }

    private void BtnSaveHtml_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentArt) || _source == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "컬러 HTML로 저장",
            Filter     = "HTML 파일|*.html",
            DefaultExt = ".html",
            FileName   = "char-art",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SetStatus("HTML 생성 중...");
            var config = BuildConfig();
            var html   = GenerateColorHtml(config);
            File.WriteAllText(dlg.FileName, html, new System.Text.UTF8Encoding(true));
            SetStatus($"HTML 저장 완료: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($"HTML 저장 실패: {ex.Message}");
        }
    }

    private string GenerateColorHtml(ArtConfig config)
    {
        double charAspect = MeasureCharAspect(config.FontFamily, config.FontSize);
        if (CharSetLibrary.IsFullWidth(config.CharSetName) && charAspect > 1.3)
            charAspect = 1.0;

        var brightness = ImageSampler.Sample(_source!, config.Columns, charAspect);
        int rows = brightness.GetLength(0);
        int cols = brightness.GetLength(1);

        var colors = ImageSampler.SampleColors(_source!, rows, cols);

        var chars = _orderedChars.Length > 0
            ? _orderedChars
            : CharSetLibrary.GetPreset(config.CharSetName) ?? CharSetLibrary.GetPreset("ASCII 기본")!;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Char.Art</title>");
        sb.AppendLine("<style>");
        sb.AppendLine($"body{{background:#1A1A1E;margin:0;padding:12px;}}");
        sb.AppendLine($"pre{{font-family:\"{config.FontFamily}\",Consolas,monospace;font-size:{config.FontSize}px;line-height:1.0;letter-spacing:0;white-space:pre;}}");
        sb.AppendLine("span{display:inline;}");
        sb.AppendLine("</style></head><body><pre>");

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float mapped = config.Invert ? brightness[r, c] : (1f - brightness[r, c]);
                int idx = Math.Clamp((int)(mapped * chars.Length), 0, chars.Length - 1);
                char ch = chars[idx];

                var (R, G, B) = colors[r, c];
                string hex = $"#{R:X2}{G:X2}{B:X2}";
                string charStr = ch switch { '<' => "&lt;", '>' => "&gt;", '&' => "&amp;", _ => ch.ToString() };
                sb.Append($"<span style=\"color:{hex}\">{charStr}</span>");
            }
            if (r < rows - 1) sb.AppendLine();
        }

        sb.AppendLine("</pre></body></html>");
        return sb.ToString();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentArt)) return;
        try
        {
            Clipboard.SetText(_currentArt);
            SetStatus("클립보드에 복사했습니다.");
        }
        catch (Exception ex)
        {
            SetStatus($"클립보드 복사 실패: {ex.Message}");
        }
    }

    // ── 도움말 ───────────────────────────────────────────────────────
    private HelpWindow? _helpWindow;

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        if (_helpWindow?.IsVisible == true)
        {
            _helpWindow.Activate();
            return;
        }
        _helpWindow = new HelpWindow { Owner = this };
        _helpWindow.Show();
    }

    // ── 생성 버튼 ────────────────────────────────────────────────────
    private void BtnGenerate_Click(object sender, RoutedEventArgs e) => GenerateNow();

    // ── 디바운스 스케줄러 ────────────────────────────────────────────
    private void ScheduleGenerate()
    {
        if (_source == null) return;

        _debounceCts.Cancel();
        _debounceCts.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(400, token).ContinueWith(_ =>
        {
            if (token.IsCancellationRequested) return;
            try { Dispatcher.Invoke(GenerateNow); } catch { }
        }, TaskScheduler.Default);
    }

    // ── 핵심 생성 로직 ────────────────────────────────────────────────
    private void GenerateNow()
    {
        if (_source == null) return;

        var config = BuildConfig();

        // 커스텀 문자 비어있을 때 피드백
        if (config.CharSetName == "커스텀" && string.IsNullOrWhiteSpace(config.CustomChars))
        {
            SetStatus("커스텀 문자를 입력해주세요. ASCII 기본 문자셋으로 대체합니다.");
        }

        SetBusy(true);
        SetStatus("생성 중...");

        if (CharSetLibrary.NeedsDynamic(config.CharSetName))
        {
            _orderedChars = CharSetLibrary.ComputeDynamic(
                config.CharSetName, config.CustomChars,
                config.FontFamily,  config.FontSize);
        }
        else
        {
            _orderedChars = CharSetLibrary.GetPreset(config.CharSetName)
                            ?? CharSetLibrary.GetPreset("ASCII 기본")!;
        }

        double charAspect = MeasureCharAspect(config.FontFamily, config.FontSize);

        var source   = _source;
        var chars    = _orderedChars;
        var cols     = config.Columns;
        var invert   = config.Invert;
        var isFullW  = CharSetLibrary.IsFullWidth(config.CharSetName);

        if (isFullW && charAspect > 1.3)
            charAspect = 1.0;

        Task.Run(() =>
        {
            try
            {
                var brightness = ImageSampler.Sample(source, cols, charAspect);
                var art        = TextArtGenerator.Generate(brightness, chars, invert);
                return art;
            }
            catch { return null; }
        }).ContinueWith(t =>
        {
            var art = t.Result;
            if (art != null)
            {
                _currentArt = art;

                TxtArt.Text           = art;
                TxtArt.FontFamily     = new FontFamily(config.FontFamily);
                _viewFontSize         = config.FontSize;
                TxtArt.FontSize       = _viewFontSize;
                TxtZoomInfo.Visibility = Visibility.Collapsed;
                TxtArtHint.Visibility = Visibility.Collapsed;

                int rows = art.Count(c => c == '\n') + 1;
                SetStatus($"완료  |  {rows} 행 × {cols} 열  |  {chars.Length} 문자  |  종횡비 {charAspect:F2}");

                BtnSave.IsEnabled     = true;
                BtnSavePng.IsEnabled  = true;
                BtnSaveHtml.IsEnabled = true;
                BtnCopy.IsEnabled     = true;
            }
            else
            {
                SetStatus("생성 중 오류가 발생했습니다.");
            }
            SetBusy(false);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private ArtConfig BuildConfig()
    {
        var fontSizeStr = CbFontSize.SelectedItem as string ?? "8pt";
        double.TryParse(fontSizeStr.Replace("pt", ""), out double fontSize);
        if (fontSize <= 0) fontSize = 8.0;

        return new ArtConfig
        {
            CharSetName = CbCharSet.SelectedItem as string ?? "ASCII 기본",
            CustomChars = TxtCustomChars.Text,
            FontFamily  = CbFont.SelectedItem as string ?? "Consolas",
            FontSize    = fontSize,
            Columns     = (int)SldCols.Value,
            Invert      = ChkInvert.IsChecked == true,
        };
    }

    private static double MeasureCharAspect(string fontFamily, double fontSize)
    {
        var ft = new System.Windows.Media.FormattedText(
            "M",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily),
            fontSize,
            System.Windows.Media.Brushes.White,
            96.0);

        double w = ft.Width;
        double h = ft.Height;
        return (w > 0) ? h / w : 2.0;
    }

    private void SetBusy(bool busy)
    {
        BtnGenerate.IsEnabled  = !busy && _source != null;
        BtnOpen.IsEnabled      = !busy;
        BtnSave.IsEnabled      = !busy && !string.IsNullOrEmpty(_currentArt);
        BtnSavePng.IsEnabled   = !busy && !string.IsNullOrEmpty(_currentArt);
        BtnSaveHtml.IsEnabled  = !busy && !string.IsNullOrEmpty(_currentArt);
        BtnCopy.IsEnabled      = !busy && !string.IsNullOrEmpty(_currentArt);
    }

    private void SetStatus(string msg)
    {
        TxtStatus.Text = msg;
    }
}
