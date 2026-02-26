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

    // 폰트 목록
    private static readonly string[] Fonts = ["Consolas", "Courier New", "굴림체", "맑은 고딕", "D2Coding"];

    // 폰트 크기 목록
    private static readonly double[] FontSizes = [6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0];

    // ── 생성자 ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // ComboBox 초기화 (Loaded 이후이므로 이벤트 안전)
        CbCharSet.ItemsSource  = CharSetLibrary.PresetNames;
        CbCharSet.SelectedIndex = 0;

        CbFont.ItemsSource  = Fonts;
        CbFont.SelectedIndex = 0;

        CbFontSize.ItemsSource  = FontSizes.Select(f => $"{f}pt").ToList();
        CbFontSize.SelectedIndex = 2; // 8pt

        UpdateColsLabel();
    }

    // ── 이벤트: 이미지 열기 ──────────────────────────────────────────
    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "이미지 열기",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.webp|모든 파일|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        LoadImage(dlg.FileName);
    }

    private void LoadImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource       = new Uri(path, UriKind.Absolute);
            bmp.CacheOption     = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            _source = bmp;

            ImgPreview.Source     = bmp;
            ImgPreview.Visibility = Visibility.Visible;
            TxtDropHint.Visibility = Visibility.Collapsed;

            BtnGenerate.IsEnabled = true;
            SetStatus($"이미지 로드: {Path.GetFileName(path)}  ({bmp.PixelWidth}×{bmp.PixelHeight})");

            ScheduleGenerate();
        }
        catch (Exception ex)
        {
            SetStatus($"이미지 로드 실패: {ex.Message}");
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

    // ── 이벤트: 설정 변경 ────────────────────────────────────────────
    private void Setting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        // 한글/한자 선택 시 굴림체 자동 전환
        var charSetName = CbCharSet.SelectedItem as string ?? "";
        if (CharSetLibrary.IsFullWidth(charSetName))
        {
            int gulimIdx = Array.IndexOf(Fonts, "굴림체");
            if (gulimIdx >= 0 && CbFont.SelectedIndex != gulimIdx)
            {
                CbFont.SelectedIndex = gulimIdx;
                return; // CbFont.SelectionChanged → 다시 호출됨
            }
        }

        // 커스텀 입력 패널 표시
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

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentArt)) return;
        Clipboard.SetText(_currentArt);
        SetStatus("클립보드에 복사했습니다.");
    }

    // ── 생성 버튼 ────────────────────────────────────────────────────
    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        GenerateNow();
    }

    // ── 디바운스 스케줄러 ────────────────────────────────────────────
    private void ScheduleGenerate()
    {
        if (_source == null) return;

        _debounceCts.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(400, token).ContinueWith(_ =>
        {
            if (token.IsCancellationRequested) return;
            Dispatcher.Invoke(GenerateNow);
        }, TaskScheduler.Default);
    }

    // ── 핵심 생성 로직 ────────────────────────────────────────────────
    private void GenerateNow()
    {
        if (_source == null) return;

        var config = BuildConfig();

        SetBusy(true);
        SetStatus("생성 중...");

        // 동적 문자셋은 UI 스레드에서 계산 후 Task.Run으로 생성
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

        // charAspect: FormattedText로 현재 폰트 종횡비 측정
        double charAspect = MeasureCharAspect(config.FontFamily, config.FontSize);

        var source    = _source;
        var chars     = _orderedChars;
        var cols      = config.Columns;
        var invert    = config.Invert;
        var isFullW   = CharSetLibrary.IsFullWidth(config.CharSetName);

        // 전각 문자는 정사각형에 가까움 → charAspect ≈ 1.0 보정
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
            catch
            {
                return null;
            }
        }).ContinueWith(t =>
        {
            var art = t.Result;
            if (art != null)
            {
                _currentArt = art;

                TxtArt.Text           = art;
                TxtArt.FontFamily     = new FontFamily(config.FontFamily);
                TxtArt.FontSize       = config.FontSize;
                TxtArtHint.Visibility = Visibility.Collapsed;

                int rows = art.Count(c => c == '\n') + 1;
                SetStatus($"완료  |  {rows} 행 × {cols} 열  |  {chars.Length} 문자  |  종횡비 {charAspect:F2}");

                BtnSave.IsEnabled = true;
                BtnCopy.IsEnabled = true;
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
            CharSetName  = CbCharSet.SelectedItem as string ?? "ASCII 기본",
            CustomChars  = TxtCustomChars.Text,
            FontFamily   = CbFont.SelectedItem as string ?? "Consolas",
            FontSize     = fontSize,
            Columns      = (int)SldCols.Value,
            Invert       = ChkInvert.IsChecked == true,
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
            Brushes.White,
            96.0);

        double w = ft.Width;
        double h = ft.Height;
        return (w > 0) ? h / w : 2.0;
    }

    private void SetBusy(bool busy)
    {
        BtnGenerate.IsEnabled = !busy && _source != null;
        BtnOpen.IsEnabled     = !busy;
    }

    private void SetStatus(string msg)
    {
        TxtStatus.Text = msg;
    }
}
