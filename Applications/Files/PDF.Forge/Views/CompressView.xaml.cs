using Microsoft.Win32;
using PdfForge.Services;

namespace PdfForge.Views;

public partial class CompressView : UserControl
{
    private readonly PdfCompressService _svc = new();
    private string? _inputPath;

    public CompressView()
    {
        InitializeComponent();
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "PDF 파일|*.pdf" };
        if (dlg.ShowDialog() != true) return;
        _inputPath = dlg.FileName;
        TxtInput.Text = _inputPath;
        TxtInput.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));

        var size = new FileInfo(_inputPath).Length;
        TxtOrigSize.Text = FormatSize(size);
        TxtResultSize.Visibility = Visibility.Collapsed;
        TxtRatio.Visibility = Visibility.Collapsed;
    }

    private async void BtnCompress_Click(object sender, RoutedEventArgs e)
    {
        if (_inputPath is null)
        {
            MessageBox.Show("PDF 파일을 선택하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "PDF 파일|*.pdf",
            FileName = Path.GetFileNameWithoutExtension(_inputPath) + "_compressed.pdf"
        };
        if (dlg.ShowDialog() != true) return;

        BtnCompress.IsEnabled = false;
        PbCompress.Visibility = Visibility.Visible;
        try
        {
            long origSize = new FileInfo(_inputPath).Length;
            long newSize = await _svc.CompressAsync(_inputPath, dlg.FileName);

            double ratio = origSize > 0 ? (1.0 - (double)newSize / origSize) * 100 : 0;
            TxtResultSize.Text = FormatSize(newSize);
            TxtResultSize.Visibility = Visibility.Visible;
            TxtRatio.Text = ratio >= 0
                ? $"압축률 {ratio:F1}% (절약: {FormatSize(origSize - newSize)})"
                : $"크기 증가: {FormatSize(newSize - origSize)}";
            TxtRatio.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"압축 실패: {ex.Message}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnCompress.IsEnabled = true;
            PbCompress.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
        >= 1_024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };
}
