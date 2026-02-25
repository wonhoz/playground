using QrForge.Models;
using QrForge.Services;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QrForge.Windows;

public partial class BatchWindow : Window
{
    private readonly QrStyle _style;
    private string _csvPath  = string.Empty;
    private string _outDir   = string.Empty;
    private CancellationTokenSource? _cts;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public BatchWindow(QrStyle style)
    {
        _style = style;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
        };
    }

    private void BtnCsv_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "CSV 파일|*.csv|모든 파일|*.*",
            Title  = "CSV 파일 선택"
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _csvPath = dlg.FileName;
            TxtCsvPath.Text = _csvPath;
            TxtCsvPath.Foreground = System.Windows.Media.Brushes.LightGray;
        }
    }

    private void BtnDir_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "출력 폴더 선택" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _outDir = dlg.SelectedPath;
            TxtOutDir.Text = _outDir;
            TxtOutDir.Foreground = System.Windows.Media.Brushes.LightGray;
        }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
        {
            System.Windows.MessageBox.Show("CSV 파일을 선택해주세요.", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrEmpty(_outDir))
        {
            System.Windows.MessageBox.Show("출력 폴더를 선택해주세요.", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        List<BatchItem> items;
        try { items = BatchService.ParseCsv(_csvPath); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"CSV 파싱 오류: {ex.Message}", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (items.Count == 0)
        {
            System.Windows.MessageBox.Show("CSV에 유효한 항목이 없습니다.", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnStart.IsEnabled = false;
        Pb.Maximum = 100;
        Pb.Value   = 0;

        _cts = new CancellationTokenSource();
        var progress = new Progress<(int done, int total, string current)>(rep =>
        {
            Pb.Value     = rep.done * 100.0 / rep.total;
            TxtStatus.Text = $"생성 중: {rep.current}";
            TxtCount.Text  = $"{rep.done} / {rep.total}";
        });

        try
        {
            await BatchService.GenerateAsync(items, _style, _outDir, progress, _cts.Token);
            TxtStatus.Text = "완료!";
            TxtCount.Text  = $"{items.Count} / {items.Count}";
            Pb.Value = 100;
            System.Windows.MessageBox.Show($"{items.Count}개 QR 코드 생성 완료!\n\n{_outDir}", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text = "취소됨";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"오류: {ex.Message}", "QR.Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnStart.IsEnabled = true;
            _cts = null;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }
}
