using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ImgCast.Services;
using SkiaSharp;

namespace ImgCast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    bool _converting = false;
    bool _allIcoUnchecked = false;
    CancellationTokenSource? _cts;
    string? _lastOutputDir;
    AppSettings _settings = new();

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _settings = AppSettings.Load();
        ApplySettings();
    }

    void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
    }

    // ─── 설정 적용 / 저장 ─────────────────────────────────────────────────
    void ApplySettings()
    {
        // 출력 포맷 복원
        foreach (System.Windows.Controls.ComboBoxItem item in CbOutput.Items)
        {
            if (item.Tag as string == _settings.OutputFormat)
            {
                CbOutput.SelectedItem = item;
                break;
            }
        }

        // 입력 포맷 복원
        foreach (System.Windows.Controls.ComboBoxItem item in CbInput.Items)
        {
            if (item.Tag as string == _settings.InputFilter)
            {
                CbInput.SelectedItem = item;
                break;
            }
        }

        // 덮어쓰기 복원
        ChkOverwrite.IsChecked = _settings.Overwrite;

        // JPG 품질 복원
        SliderQuality.Value = _settings.JpgQuality;

        // ICO 사이즈 복원
        var sizes = _settings.IcoSizes.ToHashSet();
        ChkIco16.IsChecked  = sizes.Contains(16);
        ChkIco32.IsChecked  = sizes.Contains(32);
        ChkIco48.IsChecked  = sizes.Contains(48);
        ChkIco64.IsChecked  = sizes.Contains(64);
        ChkIco128.IsChecked = sizes.Contains(128);
        ChkIco256.IsChecked = sizes.Contains(256);

        // SVG 출력 크기 복원
        if      (_settings.SvgOutputSize == 256)  RbSvg256.IsChecked  = true;
        else if (_settings.SvgOutputSize == 512)  RbSvg512.IsChecked  = true;
        else if (_settings.SvgOutputSize == 2048) RbSvg2048.IsChecked = true;
        else                                       RbSvg1024.IsChecked = true;
    }

    void SaveSettings()
    {
        _settings.OutputFormat  = GetOutputFormatTag();
        _settings.InputFilter   = GetInputFilterTag();
        _settings.Overwrite     = ChkOverwrite.IsChecked == true;
        _settings.JpgQuality    = (int)SliderQuality.Value;
        _settings.IcoSizes      = GetIcoSizes();
        _settings.SvgOutputSize = GetSvgOutputSize();
        _settings.Save();
    }

    // ─── 드래그 & 드롭 ──────────────────────────────────────────────────────
    void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.None; return; }
        var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
        bool hasSupported = dropped is { Length: > 0 } && ImageConverter.CollectFiles(dropped, GetInputFilter()).Length > 0;
        e.Effects = hasSupported ? DragDropEffects.Copy : DragDropEffects.None;
        if (hasSupported) AnimateDropZone(true);
    }

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_DragLeave(object sender, DragEventArgs e)
    {
        AnimateDropZone(false);
    }

    async void Window_Drop(object sender, DragEventArgs e)
    {
        AnimateDropZone(false);
        try
        {
            if (_converting)
            {
                SetStatus("변환 중입니다. 완료 후 다시 드롭하세요.");
                return;
            }
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0) return;

            var filter = GetInputFilter();
            var files  = ImageConverter.CollectFiles(dropped, filter);

            if (files.Length == 0)
            {
                SetStatus("지원하는 이미지 파일이 없습니다 (SVG · PNG · JPG · BMP · ICO)");
                return;
            }

            BtnOpenFolder.Visibility = Visibility.Collapsed;
            _lastOutputDir = Path.GetDirectoryName(files[0]);

            // 단일 파일이면 포맷별 미리보기 표시
            if (files.Length == 1 && files[0].EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                await ShowSvgPreviewAsync(files[0]);
            else if (files.Length == 1 && files[0].EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                await ShowIcoPreviewAsync(files[0]);
            else if (files.Length == 1 && IsBitmapPreviewable(files[0]))
                await ShowBitmapPreviewAsync(files[0]);
            else
                ShowFileSummary(files);

            await RunConversionAsync(files);
        }
        catch (Exception ex)
        {
            SetStatus($"오류: {ex.Message}");
            ResetDropZone();
        }
    }

    void AnimateDropZone(bool hover)
    {
        var target = hover ? Color.FromRgb(0x29, 0xB6, 0xF6) : Color.FromRgb(0x3A, 0x3A, 0x48);
        var anim   = new ColorAnimation(target, TimeSpan.FromMilliseconds(150));
        DropBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    // ─── 변환 실행 ───────────────────────────────────────────────────────────
    async Task RunConversionAsync(string[] files)
    {
        _converting = true;
        _cts = new CancellationTokenSource();

        PBar.Visibility    = Visibility.Visible;
        BtnCancel.Visibility = Visibility.Visible;
        PBar.Value = 0;

        var output     = GetOutputFormat();
        bool overwrite = ChkOverwrite.IsChecked == true;
        int  quality   = (int)SliderQuality.Value;
        int[] icoSizes = GetIcoSizes();
        int svgSize    = GetSvgOutputSize();

        var progress = new Progress<(int Current, int Total, string File)>(p =>
        {
            PBar.Value = p.Total > 0 ? (double)p.Current / p.Total * 100 : 0;
            SetStatus($"처리 중: {p.File}  ({p.Current} / {p.Total})");
        });

        ConversionResult? result = null;
        try
        {
            result = await ImageConverter.ConvertAsync(files, output, overwrite, quality, icoSizes, svgSize, progress, _cts.Token);
        }
        finally
        {
            _converting = false;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnCancel.IsEnabled = true;
            _cts?.Dispose();
            _cts = null;
        }

        if (result is not null)
        {
            PBar.Value = result.Cancelled ? PBar.Value : 100;
            ShowResult(result);
        }
        else
        {
            ResetDropZone();
        }
    }

    // ─── 결과 표시 ───────────────────────────────────────────────────────────
    void ShowResult(ConversionResult result)
    {
        if (result.Cancelled)
        {
            SetStatus($"변환이 취소되었습니다 — 완료 {result.Success}개 / 실패 {result.Failed}개");
            if (result.Success > 0 && _lastOutputDir is not null)
                BtnOpenFolder.Visibility = Visibility.Visible;
            ResetDropZone();
            return;
        }

        string statusMsg = $"완료 — 성공 {result.Success}개 / 실패 {result.Failed}개";
        if (result.Skipped > 0) statusMsg += $" / 스킵 {result.Skipped}개 (동일 포맷)";
        SetStatus(statusMsg);

        if (result.Success > 0 && _lastOutputDir is not null)
            BtnOpenFolder.Visibility = Visibility.Visible;

        if (result.Failed > 0)
        {
            string msg = $"✅ 성공: {result.Success}개\n❌ 실패: {result.Failed}개\n\n실패 목록:\n";
            foreach (var (file, err) in result.Errors)
                msg += $"• {Path.GetFileName(file)}\n  {err}\n";
            MessageBox.Show(msg.TrimEnd(), "변환 완료 (일부 실패)", MessageBoxButton.OK, MessageBoxImage.Warning);
            ResetDropZone();
        }
        else if (result.Skipped > 0 && result.Success == 0)
        {
            MessageBox.Show($"모든 파일이 이미 출력 포맷과 동일하여 스킵되었습니다.\n출력 포맷을 변경해 주세요.",
                "스킵됨", MessageBoxButton.OK, MessageBoxImage.Information);
            ResetDropZone();
        }
        else if (SvgPreviewPanel.Visibility == Visibility.Visible)
        {
            // 미리보기가 표시된 단일 파일 완전 성공 — 미리보기 유지, PBar만 숨김
            PBar.Visibility = Visibility.Collapsed;
            PBar.Value = 0;
        }
        else
        {
            ResetDropZone();
        }
    }

    // ─── ICO 미리보기 ────────────────────────────────────────────────────────
    async Task ShowIcoPreviewAsync(string icoPath)
    {
        SetStatus("미리보기 렌더링 중…");
        using var bitmap = await ImageConverter.RenderIcoPreviewAsync(icoPath, 240);
        if (bitmap is null) { ShowFileSummary([icoPath]); return; }

        SvgPreviewImage.Source = BitmapToImageSource(bitmap);
        TbSvgFileName.Text = Path.GetFileName(icoPath);
        TbSvgSubInfo.Text  = "ICO 미리보기 · 1개 파일";

        DropHint.Visibility        = Visibility.Collapsed;
        FileSummary.Visibility     = Visibility.Collapsed;
        SvgPreviewPanel.Visibility = Visibility.Visible;
        SetStatus("파일을 드롭하면 자동으로 변환을 시작합니다");
    }

    // ─── PNG/JPG/BMP 미리보기 ────────────────────────────────────────────────
    static bool IsBitmapPreviewable(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    async Task ShowBitmapPreviewAsync(string path)
    {
        SetStatus("미리보기 렌더링 중…");
        using var bitmap = await ImageConverter.RenderBitmapPreviewAsync(path, 240);
        if (bitmap is null) { ShowFileSummary([path]); return; }

        var ext = Path.GetExtension(path).ToUpperInvariant().TrimStart('.');
        SvgPreviewImage.Source = BitmapToImageSource(bitmap);
        TbSvgFileName.Text = Path.GetFileName(path);
        TbSvgSubInfo.Text  = $"{ext} 미리보기 · 1개 파일";

        DropHint.Visibility        = Visibility.Collapsed;
        FileSummary.Visibility     = Visibility.Collapsed;
        SvgPreviewPanel.Visibility = Visibility.Visible;
        SetStatus("파일을 드롭하면 자동으로 변환을 시작합니다");
    }

    // ─── SVG 미리보기 ────────────────────────────────────────────────────────
    async Task ShowSvgPreviewAsync(string svgPath)
    {
        SetStatus($"미리보기 렌더링 중…");
        using var bitmap = await ImageConverter.RenderPreviewAsync(svgPath, 240);
        if (bitmap is null) { ShowFileSummary([svgPath]); return; }

        SvgPreviewImage.Source = BitmapToImageSource(bitmap);
        TbSvgFileName.Text = Path.GetFileName(svgPath);
        TbSvgSubInfo.Text  = "SVG 미리보기 · 1개 파일";

        DropHint.Visibility       = Visibility.Collapsed;
        FileSummary.Visibility    = Visibility.Collapsed;
        SvgPreviewPanel.Visibility = Visibility.Visible;
        SetStatus("파일을 드롭하면 자동으로 변환을 시작합니다");
    }

    static BitmapSource BitmapToImageSource(SKBitmap bitmap)
    {
        using var img  = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var ms   = new MemoryStream(data.ToArray());
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption  = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ─── UI 헬퍼 ─────────────────────────────────────────────────────────────
    void ShowFileSummary(string[] files)
    {
        DropHint.Visibility        = Visibility.Collapsed;
        SvgPreviewPanel.Visibility = Visibility.Collapsed;
        FileSummary.Visibility     = Visibility.Visible;

        TbFileCount.Text = files.Length.ToString();
        TbFileLabel.Text = "개 파일 처리 예정";
        TbFileSample.Text = files.Length <= 3
            ? string.Join("\n", files.Select(Path.GetFileName))
            : string.Join("\n", files.Take(2).Select(Path.GetFileName)) + $"\n...외 {files.Length - 2}개";
    }

    void ResetDropZone()
    {
        DropHint.Visibility        = Visibility.Visible;
        FileSummary.Visibility     = Visibility.Collapsed;
        SvgPreviewPanel.Visibility = Visibility.Collapsed;
        PBar.Visibility            = Visibility.Collapsed;
        PBar.Value = 0;
    }

    void SetStatus(string text) => TbStatus.Text = text;

    // ─── 이벤트 핸들러 ───────────────────────────────────────────────────────
    void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.IsEnabled = false;
        SetStatus("취소 중…");
    }

    void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }

    void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && _converting)
        {
            BtnCancel_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.OemQuestion)
        {
            HelpPopup.IsOpen = !HelpPopup.IsOpen;
            e.Handled = true;
        }
    }

    void ChkIco_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool anyChecked = ChkIco16.IsChecked == true || ChkIco32.IsChecked == true ||
                          ChkIco48.IsChecked == true || ChkIco64.IsChecked == true ||
                          ChkIco128.IsChecked == true || ChkIco256.IsChecked == true;
        if (!anyChecked)
        {
            _allIcoUnchecked = true;
            SetStatus("모든 사이즈가 해제됨 — 변환 시 기본 사이즈 전체 사용 (16·32·48·64·128·256)");
        }
    }

    void ChkIco_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_allIcoUnchecked)
        {
            _allIcoUnchecked = false;
            SetStatus("파일을 드롭하면 자동으로 변환을 시작합니다");
        }
    }

    void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutputDir is not null && Directory.Exists(_lastOutputDir))
            Process.Start(new ProcessStartInfo("explorer.exe", _lastOutputDir) { UseShellExecute = true });
    }

    void CbOutput_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var fmt = GetOutputFormat();
        JpgQualityPanel.Visibility = fmt == OutputFormat.JPG ? Visibility.Visible  : Visibility.Collapsed;
        IcoSizesPanel.Visibility   = fmt == OutputFormat.ICO ? Visibility.Visible  : Visibility.Collapsed;
        SvgSizePanel.Visibility    = fmt != OutputFormat.ICO ? Visibility.Visible  : Visibility.Collapsed;
    }

    void SliderQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        TbQualityVal.Text = ((int)e.NewValue).ToString();
    }

    // ─── 값 추출 헬퍼 ────────────────────────────────────────────────────────
    InputFilter GetInputFilter()
    {
        var tag = GetInputFilterTag();
        return tag switch
        {
            "SVG" => InputFilter.SVG,
            "PNG" => InputFilter.PNG,
            "JPG" => InputFilter.JPG,
            "BMP" => InputFilter.BMP,
            "ICO" => InputFilter.ICO,
            _     => InputFilter.All
        };
    }

    string GetInputFilterTag() =>
        (CbInput.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "All";

    OutputFormat GetOutputFormat()
    {
        var tag = GetOutputFormatTag();
        return tag switch
        {
            "PNG" => OutputFormat.PNG,
            "JPG" => OutputFormat.JPG,
            "BMP" => OutputFormat.BMP,
            _     => OutputFormat.ICO
        };
    }

    string GetOutputFormatTag() =>
        (CbOutput.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "ICO";

    int GetSvgOutputSize()
    {
        if (RbSvg256.IsChecked  == true) return 256;
        if (RbSvg512.IsChecked  == true) return 512;
        if (RbSvg2048.IsChecked == true) return 2048;
        return 1024;
    }

    int[] GetIcoSizes()
    {
        var sizes = new List<int>();
        if (ChkIco16.IsChecked  == true) sizes.Add(16);
        if (ChkIco32.IsChecked  == true) sizes.Add(32);
        if (ChkIco48.IsChecked  == true) sizes.Add(48);
        if (ChkIco64.IsChecked  == true) sizes.Add(64);
        if (ChkIco128.IsChecked == true) sizes.Add(128);
        if (ChkIco256.IsChecked == true) sizes.Add(256);
        return sizes.Count > 0 ? [.. sizes] : [16, 32, 48, 64, 128, 256];
    }
}
