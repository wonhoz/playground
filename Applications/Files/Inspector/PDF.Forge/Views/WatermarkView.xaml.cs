using Microsoft.Win32;
using PdfForge.Services;

namespace PdfForge.Views;

public partial class WatermarkView : UserControl
{
    private readonly PdfWatermarkService _svc = new();
    private string? _inputPath;
    private string? _imagePath;

    public WatermarkView()
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
    }

    private void SelectImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dlg.ShowDialog() != true) return;
        _imagePath = dlg.FileName;
        TxtImagePath.Text = _imagePath;
        TxtImagePath.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
    }

    private void Type_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        TextPanel.Visibility = RbText.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ImagePanel.Visibility = RbImage.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        TxtOpacityVal.Text = $"{SliderOpacity.Value * 100:F0}%";
    }

    private void Scale_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        TxtScaleVal.Text = $"{SliderScale.Value * 100:F0}%";
    }

    private async void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_inputPath is null)
        {
            MessageBox.Show("PDF 파일을 선택하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "PDF 파일|*.pdf",
            FileName = Path.GetFileNameWithoutExtension(_inputPath) + "_watermarked.pdf"
        };
        if (dlg.ShowDialog() != true) return;

        BtnApply.IsEnabled = false;
        PbWatermark.Visibility = Visibility.Visible;
        try
        {
            if (RbText.IsChecked == true)
            {
                var text = TxtWatermark.Text;
                double fontSize = double.TryParse(TxtFontSize.Text, out var fs) ? fs : 64;
                int rotation = int.TryParse(TxtRotation.Text, out var rot) ? rot : -45;
                byte r = byte.TryParse(TxtR.Text, out var rv) ? rv : (byte)128;
                byte g = byte.TryParse(TxtG.Text, out var gv) ? gv : (byte)128;
                byte b = byte.TryParse(TxtB.Text, out var bv) ? bv : (byte)128;
                double opacity = SliderOpacity.Value;

                await Task.Run(() => _svc.AddTextWatermark(_inputPath, dlg.FileName,
                    text, fontSize, rotation, r, g, b, opacity));
            }
            else
            {
                if (_imagePath is null)
                {
                    MessageBox.Show("이미지 파일을 선택하세요.", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                double scale = SliderScale.Value;
                await Task.Run(() => _svc.AddImageWatermark(_inputPath, dlg.FileName, _imagePath, scale));
            }

            MessageBox.Show($"워터마크 적용 완료!\n{dlg.FileName}", "PDF Forge",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"워터마크 적용 실패: {ex.Message}", "PDF Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnApply.IsEnabled = true;
            PbWatermark.Visibility = Visibility.Collapsed;
        }
    }
}
