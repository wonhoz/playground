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
    double _sliderX = 0.5;

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();
        Loaded   += OnLoaded;
        Drop     += OnDrop;
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        KeyDown  += OnKeyDown;
        ViewerGrid.SizeChanged += (_, _) => UpdateSlider();
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
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var img = paths.FirstOrDefault(IsImageFile);
            if (img is not null) _vm.LoadImage(img);
        }
    }

    static bool IsImageFile(string p) =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp" }
            .Contains(Path.GetExtension(p));

    // ── 키보드 ───────────────────────────────────────────────────────────
    void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _vm.LoadFromClipboard();
            e.Handled = true;
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
        if (dlg.ShowDialog() == true) _vm.SaveResultDirect(dlg.FileName);
    }

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
        if (!_vm.HasResult) return;
        var pos = e.GetPosition(ViewerGrid);
        _sliderX = Math.Clamp(pos.X / ViewerGrid.ActualWidth, 0, 1);
        UpdateSlider();
    }

    void Viewer_MouseLeave(object s, MouseEventArgs e) { }

    void UpdateSlider()
    {
        if (!IsLoaded) return;
        double w = ViewerGrid.ActualWidth;
        double h = ViewerGrid.ActualHeight;
        double x = _sliderX * w;

        // 클립 영역: 결과 이미지 왼쪽
        ResultClip.Rect = new System.Windows.Rect(0, 0, x, h);

        // 슬라이더 라인
        SliderLine.Margin = new Thickness(x - 1, 0, 0, 0);

        // 슬라이더 핸들
        SliderHandle.Margin = new Thickness(x - 16, 0, 0, 0);
    }
}
