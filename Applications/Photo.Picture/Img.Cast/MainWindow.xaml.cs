using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ImgCast.Services;

namespace ImgCast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    bool _converting = false;
    CancellationTokenSource? _cts;

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
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

    async void Window_Drop(object sender, DragEventArgs e)
    {
        AnimateDropZone(false);
        if (_converting) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] dropped || dropped.Length == 0) return;

        var filter = GetInputFilter();
        var files  = ImageConverter.CollectFiles(dropped, filter);

        if (files.Length == 0)
        {
            SetStatus("지원하는 이미지 파일이 없습니다 (SVG / PNG / JPG / BMP)");
            return;
        }

        ShowFileSummary(files);
        await RunConversionAsync(files);
    }

    void AnimateDropZone(bool hover)
    {
        var target = hover ? Color.FromRgb(0x6E, 0x8E, 0xFA) : Color.FromRgb(0x3A, 0x3A, 0x48);
        var anim   = new ColorAnimation(target, TimeSpan.FromMilliseconds(150));
        DropBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    // ─── 변환 실행 ───────────────────────────────────────────────────────────
    async Task RunConversionAsync(string[] files)
    {
        _converting = true;
        _cts = new CancellationTokenSource();

        PBar.Visibility = Visibility.Visible;
        PBar.Value = 0;

        var output     = GetOutputFormat();
        bool overwrite = ChkOverwrite.IsChecked == true;
        int  quality   = (int)SliderQuality.Value;

        var progress = new Progress<(int Current, int Total, string File)>(p =>
        {
            PBar.Value = p.Total > 0 ? (double)p.Current / p.Total * 100 : 0;
            SetStatus($"처리 중: {p.File}  ({p.Current} / {p.Total})");
        });

        ConversionResult? result = null;
        try
        {
            result = await ImageConverter.ConvertAsync(files, output, overwrite, quality, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            SetStatus("변환이 취소되었습니다");
        }
        finally
        {
            _converting = false;
            _cts?.Dispose();
            _cts = null;
            PBar.Value = 100;
        }

        if (result is not null)
            ShowResult(result);
    }

    // ─── 결과 다이얼로그 ─────────────────────────────────────────────────────
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

        string title = result.Failed == 0 ? "변환 완료" : "변환 완료 (일부 실패)";
        MessageBox.Show(msg.TrimEnd(), title, MessageBoxButton.OK,
            result.Failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

        ResetDropZone();
    }

    // ─── UI 헬퍼 ─────────────────────────────────────────────────────────────
    void ShowFileSummary(string[] files)
    {
        DropHint.Visibility    = Visibility.Collapsed;
        FileSummary.Visibility = Visibility.Visible;

        TbFileCount.Text = files.Length.ToString();
        TbFileLabel.Text = files.Length == 1 ? "개 파일 처리 예정" : "개 파일 처리 예정";
        TbFileSample.Text = files.Length <= 3
            ? string.Join("\n", files.Select(Path.GetFileName))
            : string.Join("\n", files.Take(2).Select(Path.GetFileName)) + $"\n...외 {files.Length - 2}개";
    }

    void ResetDropZone()
    {
        DropHint.Visibility    = Visibility.Visible;
        FileSummary.Visibility = Visibility.Collapsed;
        PBar.Visibility        = Visibility.Collapsed;
        PBar.Value             = 0;
    }

    void SetStatus(string text) => TbStatus.Text = text;

    InputFilter GetInputFilter()
    {
        var tag = (CbInput.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "All";
        return tag switch
        {
            "SVG" => InputFilter.SVG,
            "PNG" => InputFilter.PNG,
            "JPG" => InputFilter.JPG,
            "BMP" => InputFilter.BMP,
            _     => InputFilter.All
        };
    }

    OutputFormat GetOutputFormat()
    {
        var tag = (CbOutput.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "ICO";
        return tag switch
        {
            "PNG" => OutputFormat.PNG,
            "JPG" => OutputFormat.JPG,
            "BMP" => OutputFormat.BMP,
            _     => OutputFormat.ICO
        };
    }

    // ─── 이벤트 ──────────────────────────────────────────────────────────────
    void CbInput_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }

    void CbOutput_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var fmt = GetOutputFormat();
        JpgQualityPanel.Visibility = fmt == OutputFormat.JPG ? Visibility.Visible : Visibility.Collapsed;
    }

    void SliderQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        TbQualityVal.Text = ((int)e.NewValue).ToString();
    }
}
