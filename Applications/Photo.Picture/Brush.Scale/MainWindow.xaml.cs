using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Brush.Scale;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;
    readonly RectangleGeometry _resultClip = new();
    double _sliderX = 0.5;

    // ── 줌/패닝 ───────────────────────────────────────────────────────────
    double _viewerScale = 1.0;
    double _viewerTx = 0, _viewerTy = 0;
    bool _isDragging = false;
    Point _dragStart;
    double _dragStartTx, _dragStartTy;
    readonly ScaleTransform     _imgScale     = new(1, 1);
    readonly TranslateTransform _imgTranslate = new();

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();
        ImgResult.Clip = _resultClip;

        // 이미지 컨테이너 트랜스폼 설정
        var tg = new TransformGroup();
        tg.Children.Add(_imgScale);
        tg.Children.Add(_imgTranslate);
        ImageWrapper.RenderTransform = tg;

        // 배치 완료 — MessageBox 대신 상태바 메시지 사용 (ViewModel에서 StatusText 설정)

        Loaded   += OnLoaded;
        Drop     += OnDrop;
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        KeyDown  += OnKeyDown;
        ViewerGrid.SizeChanged += (_, _) => UpdateSlider();
        Closed += (_, _) => _vm.Dispose();
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var h = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
        UpdateSlider();
    }

    // ── 드래그&드롭 ──────────────────────────────────────────────────────
    void OnDrop(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var images = paths.Where(IsImageFile).ToList();
        if (images.Count == 0) return;
        if (images.Count >= 2)
        {
            // 다중 파일 → 배치 모드 자동 진입
            var dir = Path.GetDirectoryName(images[0]);
            if (dir is not null)
            {
                _vm.BatchInputDir = dir;
                _vm.StatusText    = $"{images.Count}개 파일 감지 — 배치 모드: 입력 폴더 자동 설정됨";
                LeftScroll.ScrollToEnd();
            }
        }
        else
        {
            _vm.LoadImage(images[0]);
        }
    }

    static bool IsImageFile(string p) =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp" }
            .Contains(Path.GetExtension(p));

    // ── 키보드 ───────────────────────────────────────────────────────────
    async void OnKeyDown(object s, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        switch (e.Key)
        {
            case Key.V when ctrl:
                _vm.LoadFromClipboard();
                e.Handled = true;
                break;
            case Key.C when ctrl && _vm.HasResult:
                _vm.CopyResultToClipboard();
                e.Handled = true;
                break;
            case Key.O when ctrl:
                BtnOpen_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.S when ctrl && _vm.HasResult:
                BtnSave_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.Return when _vm.IsIdle && _vm.HasImage:
                await _vm.RunUpscaleAsync();
                _sliderX = 0.5;
                UpdateSlider();
                e.Handled = true;
                break;
            case Key.Escape:
                _vm.CancelCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space when _vm.HasResult:
                _sliderX = _sliderX > 0.5 ? 0.0 : 1.0;
                UpdateSlider();
                e.Handled = true;
                break;
            case Key.F1:
                BtnHelp_Click(null!, null!);
                e.Handled = true;
                break;
        }
    }

    // ── 툴바 버튼 ─────────────────────────────────────────────────────────
    void BtnOpen_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "이미지 파일 선택",
            Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif;*.webp|모든 파일|*.*",
        };
        if (dlg.ShowDialog() == true) _vm.LoadImage(dlg.FileName);
    }

    void BtnPaste_Click(object s, RoutedEventArgs e) => _vm.LoadFromClipboard();

    async void BtnRun_Click(object s, RoutedEventArgs e)
    {
        await _vm.RunUpscaleAsync();
        _sliderX = 0.5;
        UpdateSlider();
    }

    void BtnSave_Click(object s, RoutedEventArgs e)
    {
        if (_vm.SelectedFormat is null) return;
        var dlg = new SaveFileDialog
        {
            Title  = "결과 저장",
            Filter = _vm.SelectedFormat.Format.Filter() + "|모든 파일|*.*",
            FileName = Path.GetFileNameWithoutExtension(_vm.InputFileName) +
                       $"_{_vm.ScaleFactor}x" + _vm.SelectedFormat.Format.Extension(),
        };
        if (dlg.ShowDialog() == true)
        {
            _vm.SaveResultDirect(dlg.FileName);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"/select,\"{dlg.FileName}\"",
                UseShellExecute = true,
            });
        }
    }

    void BtnReUpscale_Click(object s, RoutedEventArgs e) => _vm.ReUpscaleResult();

    void BtnCopy_Click(object s, RoutedEventArgs e) => _vm.CopyResultToClipboard();

    void BtnHelp_Click(object s, RoutedEventArgs e)
        => new HelpWindow { Owner = this }.ShowDialog();

    void BtnOpenModelDir_Click(object s, RoutedEventArgs e) => ModelManager.OpenModelDir();
    void BtnRefreshModels_Click(object s, RoutedEventArgs e) => _vm.RefreshModelStatus();

    void BtnBatchIn_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "배치 입력 폴더 선택" };
        if (dlg.ShowDialog() == true) _vm.BatchInputDir = dlg.FolderName;
    }

    void BtnBatchOut_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "배치 출력 폴더 선택" };
        if (dlg.ShowDialog() == true) _vm.BatchOutputDir = dlg.FolderName;
    }

    async void BtnBatch_Click(object s, RoutedEventArgs e) => await _vm.RunBatchAsync();

    // ── 슬라이더 비교 뷰 ────────────────────────────────────────────────
    void Viewer_MouseMove(object s, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(ViewerGrid);
            var delta = pos - _dragStart;
            _viewerTx = _dragStartTx + delta.X;
            _viewerTy = _dragStartTy + delta.Y;
            _imgTranslate.X = _viewerTx;
            _imgTranslate.Y = _viewerTy;
            return;
        }

        if (!_vm.HasResult) return;
        var viewerPos = e.GetPosition(ViewerGrid);
        _sliderX = Math.Clamp(viewerPos.X / ViewerGrid.ActualWidth, 0, 1);
        UpdateSlider();
    }

    void Viewer_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ResetView(); return; }
        _isDragging   = true;
        _dragStart    = e.GetPosition(ViewerGrid);
        _dragStartTx  = _viewerTx;
        _dragStartTy  = _viewerTy;
        ViewerGrid.CaptureMouse();
        e.Handled = true;
    }

    void Viewer_MouseLeftButtonUp(object s, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ViewerGrid.ReleaseMouseCapture();
    }

    void Viewer_MouseWheel(object s, MouseWheelEventArgs e)
    {
        double factor   = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        double newScale = Math.Clamp(_viewerScale * factor, 0.5, 10.0);

        var pivot = e.GetPosition(ViewerGrid);
        _viewerTx   = _viewerTx + (1 - newScale / _viewerScale) * pivot.X;
        _viewerTy   = _viewerTy + (1 - newScale / _viewerScale) * pivot.Y;
        _viewerScale = newScale;

        _imgScale.ScaleX    = _viewerScale;
        _imgScale.ScaleY    = _viewerScale;
        _imgTranslate.X     = _viewerTx;
        _imgTranslate.Y     = _viewerTy;

        UpdateSlider();
        UpdateZoomHint();
        e.Handled = true;
    }

    void ResetView()
    {
        _viewerScale        = 1.0;
        _viewerTx           = 0;
        _viewerTy           = 0;
        _imgScale.ScaleX    = 1;
        _imgScale.ScaleY    = 1;
        _imgTranslate.X     = 0;
        _imgTranslate.Y     = 0;
        _sliderX            = 0.5;
        UpdateSlider();
        UpdateZoomHint();
    }

    void UpdateSlider()
    {
        if (!IsLoaded) return;
        double w = ViewerGrid.ActualWidth;
        double h = ViewerGrid.ActualHeight;

        // 화면 좌표 기준 슬라이더 X
        double screenX = _sliderX * w;

        // 이미지 컨테이너 로컬 좌표로 변환 (클립 적용)
        double localX = _viewerScale > 0 ? (screenX - _viewerTx) / _viewerScale : screenX;

        _resultClip.Rect        = new Rect(0, 0, localX, ImageWrapper.ActualHeight);
        SliderLine.Margin       = new Thickness(screenX - 1, 0, 0, 0);
        SliderHandle.Margin     = new Thickness(screenX - 16, 0, 0, 0);
    }

    void UpdateZoomHint()
    {
        if (ZoomHint is null) return;
        ZoomHint.Text = _viewerScale is >= 0.99 and <= 1.01
            ? ""
            : $"{_viewerScale:F1}×  더블클릭으로 초기화";
    }
}
