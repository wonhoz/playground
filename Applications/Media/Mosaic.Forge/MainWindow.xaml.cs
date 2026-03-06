using System.Windows.Forms;
using System.Windows.Interop;
using Mosaic.Forge.ViewModels;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;
using WinDlg  = Microsoft.Win32;

namespace Mosaic.Forge;

public partial class MainWindow : Window
{
    readonly MainViewModel      _vm     = new();
    readonly MosaicEngine       _engine = new();
    CancellationTokenSource?    _cts;
    Drawing.Bitmap?             _targetBitmap;
    Drawing.Bitmap?             _resultBitmap;
    List<TileEntry>             _tiles  = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded     += (_, _) => ApplyDarkTitle();
    }

    // ── 다크 타이틀바 ─────────────────────────────────────────────────────────

    void ApplyDarkTitle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(handle, 20, ref val, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // ── 대상 이미지 선택 ──────────────────────────────────────────────────────

    void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => PickTargetImage();

    void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
                    ? System.Windows.DragDropEffects.Copy
                    : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files
            || files.Length == 0) return;
        LoadTargetImage(files[0]);
    }

    void PickTargetImage()
    {
        var dlg = new WinDlg.OpenFileDialog
        {
            Title  = "대상 이미지 선택",
            Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif;*.gif;*.webp"
        };
        if (dlg.ShowDialog() == true)
            LoadTargetImage(dlg.FileName);
    }

    void LoadTargetImage(string path)
    {
        try
        {
            _targetBitmap?.Dispose();
            _targetBitmap = null;

            using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var src       = new Drawing.Bitmap(fs);
            _targetBitmap = new Drawing.Bitmap(src.Width, src.Height,
                                Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g   = Drawing.Graphics.FromImage(_targetBitmap);
            g.DrawImage(src, 0, 0, src.Width, src.Height);
            src.Dispose();

            // 썸네일 표시
            ImgThumb.Source = MosaicEngine.ToBitmapSource(_targetBitmap);
            ImgThumb.Visibility = Visibility.Visible;
            PnlDropHint.Visibility = Visibility.Collapsed;

            _vm.TargetPath      = path;
            _vm.TargetSizeText  = $"{_targetBitmap.Width} × {_targetBitmap.Height}px";
            TxtTargetInfo.Text  = $"{System.IO.Path.GetFileName(path)}  ({_vm.TargetSizeText})";
            _vm.UpdateOutputSize(_targetBitmap.Width, _targetBitmap.Height);
            _vm.StatusText = $"대상 이미지 로드 완료: {_vm.TargetSizeText}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"이미지 로드 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 소스 폴더 선택 ────────────────────────────────────────────────────────

    async void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description  = "소스 이미지 폴더 선택",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        string folder = dlg.SelectedPath;
        _vm.SourceFolderPath = folder;
        _vm.IsScanning       = true;
        _vm.StatusText       = "소스 이미지 스캔 중...";
        _vm.Progress         = 0;
        _vm.ProgressMax      = 100;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            var prog = new Progress<(int Done, int Total)>(p =>
            {
                _vm.ProgressMax = p.Total;
                _vm.Progress    = p.Done;
                _vm.StatusText  = $"스캔 중... {p.Done}/{p.Total}";
            });

            _tiles = await _engine.ScanFolderAsync(folder, prog, _cts.Token);
            _vm.SourceTileCount = _tiles.Count;
            _vm.StatusText      = $"소스 스캔 완료: {_tiles.Count:N0}개 이미지";
        }
        catch (OperationCanceledException) { _vm.StatusText = "스캔 취소됨"; }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"폴더 스캔 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.StatusText = "스캔 오류";
        }
        finally
        {
            _vm.IsScanning  = false;
            _vm.Progress    = 0;
        }
    }

    // ── 슬라이더 변경 ─────────────────────────────────────────────────────────

    void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        if (_targetBitmap != null)
            _vm.UpdateOutputSize(_targetBitmap.Width, _targetBitmap.Height);
    }

    // ── 생성 ──────────────────────────────────────────────────────────────────

    async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_targetBitmap == null || _tiles.Count == 0) return;

        _resultBitmap?.Dispose();
        _resultBitmap = null;
        _vm.HasResult     = false;
        ImgResult.Source  = null;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _vm.IsGenerating = true;
        _vm.Progress     = 0;
        _vm.ProgressMax  = 100;
        _vm.StatusText   = "모자이크 생성 시작...";

        var opts = new MosaicOptions(_vm.TileSize, _vm.MaxReuse);

        try
        {
            var prog = new Progress<(int Done, int Total, string Phase)>(p =>
            {
                _vm.StatusText  = p.Phase;
                _vm.ProgressMax = p.Total > 0 ? p.Total : 1;
                _vm.Progress    = p.Done;
                if (p.Total > 0)
                    TxtProgressPct.Text = $"{p.Done}/{p.Total}  ({(double)p.Done / p.Total:P0})";
            });

            _resultBitmap = await _engine.GenerateAsync(
                _targetBitmap, _tiles, opts, prog, _cts.Token);

            ImgResult.Source = MosaicEngine.ToBitmapSource(_resultBitmap);
            _vm.HasResult    = true;
            _vm.StatusText   = $"완료  —  {_resultBitmap.Width} × {_resultBitmap.Height}px";
        }
        catch (OperationCanceledException)
        {
            _vm.StatusText = "생성 취소됨";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"생성 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.StatusText = "생성 오류";
        }
        finally
        {
            _vm.IsGenerating = false;
            _vm.Progress     = 0;
            TxtProgressPct.Text = "";
        }
    }

    // ── 취소 ──────────────────────────────────────────────────────────────────

    void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    // ── 내보내기 ──────────────────────────────────────────────────────────────

    void ExportPng_Click(object sender, RoutedEventArgs e)
        => ExportResult(Imaging.ImageFormat.Png, "PNG 이미지|*.png", ".png");

    void ExportJpg_Click(object sender, RoutedEventArgs e)
        => ExportResult(Imaging.ImageFormat.Jpeg, "JPEG 이미지|*.jpg", ".jpg");

    void ExportResult(Imaging.ImageFormat fmt, string filter, string ext)
    {
        if (_resultBitmap == null) return;

        var dlg = new WinDlg.SaveFileDialog
        {
            Title      = "모자이크 저장",
            Filter     = filter,
            DefaultExt = ext,
            FileName   = $"mosaic_{DateTime.Now:yyyyMMdd_HHmmss}{ext}"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _resultBitmap.Save(dlg.FileName, fmt);
            _vm.StatusText = $"저장 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"저장 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 윈도우 닫기 ───────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _targetBitmap?.Dispose();
        _resultBitmap?.Dispose();
        base.OnClosed(e);
    }
}
