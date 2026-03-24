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
    }

    void SaveSettings()
    {
        _settings.OutputFormat = GetOutputFormatTag();
        _settings.InputFilter  = GetInputFilterTag();
        _settings.Overwrite    = ChkOverwrite.IsChecked == true;
        _settings.JpgQuality   = (int)SliderQuality.Value;
        _settings.IcoSizes     = GetIcoSizes();
        _settings.Save();
    }

    // ─── 드래그 & 드롭 ──────────────────────────────────────────────────────
    void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.None; return; }
        e.Effects = DragDropEffects.Copy;
        AnimateDropZone(true);
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

        // SVG 단일 파일이면 미리보기 표시 후 변환
        var svgFiles = files.Where(f => f.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (svgFiles.Length == 1 && files.Length == 1)
            await ShowSvgPreviewAsync(svgFiles[0]);
        else
            ShowFileSummary(files);

        await RunConversionAsync(files);
    }

    void AnimateDropZone(bool hover)
    {
        var target = hover ? Color.FromRgb(0xFF, 0x70, 0x43) : Color.FromRgb(0x3A, 0x3A, 0x48);
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

        var progress = new Progress<(int Current, int Total, string File)>(p =>
        {
            PBar.Value = p.Total > 0 ? (double)p.Current / p.Total * 100 : 0;
            SetStatus($"처리 중: {p.File}  ({p.Current} / {p.Total})");
        });

        ConversionResult? result = null;
        try
        {
            result = await ImageConverter.ConvertAsync(files, output, overwrite, quality, icoSizes, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            SetStatus("변환이 취소되었습니다");
        }
        finally
        {
            _converting = false;
            BtnCancel.Visibility = Visibility.Collapsed;
            _cts?.Dispose();
            _cts = null;
        }

        if (result is not null)
        {
            PBar.Value = 100;
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
        string msg = $"✅ 성공: {result.Success}개\n❌ 실패: {result.Failed}개";

        if (result.Errors.Count > 0)
        {
            msg += "\n\n실패 목록:\n";
            foreach (var (file, err) in result.Errors)
                msg += $"• {Path.GetFileName(file)}\n  {err}\n";
        }

        SetStatus($"완료 — 성공 {result.Success}개 / 실패 {result.Failed}개");

        if (_lastOutputDir is not null)
            BtnOpenFolder.Visibility = Visibility.Visible;

        string title = result.Failed == 0 ? "변환 완료" : "변환 완료 (일부 실패)";
        MessageBox.Show(msg.TrimEnd(), title, MessageBoxButton.OK,
            result.Failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

        ResetDropZone();
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
