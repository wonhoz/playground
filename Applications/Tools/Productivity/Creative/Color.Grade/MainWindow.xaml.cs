using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Color.Grade;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;
    CancellationTokenSource? _batchCts;

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();
        Loaded   += OnLoaded;
        Drop     += OnDrop;
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        // LUT 목록 초기 선택
        if (LutList.Items.Count > 0) LutList.SelectedIndex = 0;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var h = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
        UpdateDropHint();
    }

    // ── 드래그&드롭 ─────────────────────────────────────────────────────

    void OnDrop(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            var img = paths.FirstOrDefault(IsImageFile);
            if (img != null) { _vm.LoadImage(img); UpdateDropHint(); }
            var lut = paths.FirstOrDefault(p => p.EndsWith(".cube", StringComparison.OrdinalIgnoreCase));
            if (lut != null) _vm.AddCustomLut(lut);
        }
    }

    static bool IsImageFile(string p) =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif" }
            .Contains(Path.GetExtension(p));

    // ── 툴바 ─────────────────────────────────────────────────────────────

    void BtnOpen_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "이미지 파일 선택",
            Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif;*.gif|모든 파일|*.*",
        };
        if (dlg.ShowDialog() == true) { _vm.LoadImage(dlg.FileName); UpdateDropHint(); }
    }

    async void BtnExport_Click(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title  = "내보내기",
            Filter = "JPEG|*.jpg|PNG|*.png|BMP|*.bmp|TIFF|*.tif",
            FileName = Path.GetFileNameWithoutExtension(_vm.CurrentPath) + "_graded",
        };
        if (dlg.ShowDialog() != true) return;
        await _vm.ExportAsync(dlg.FileName, (int)SlQuality.Value);
    }

    async void BtnBatch_Click(object s, RoutedEventArgs e)
    {
        var inDlg = new OpenFolderDialog { Title = "입력 폴더 선택" };
        if (inDlg.ShowDialog() != true) return;
        var outDlg = new OpenFolderDialog { Title = "출력 폴더 선택" };
        if (outDlg.ShowDialog() != true) return;

        _batchCts = new CancellationTokenSource();
        BtnBatch.IsEnabled = false;
        await _vm.BatchExportAsync(inDlg.FolderName, outDlg.FolderName, ".jpg",
                                   (int)SlQuality.Value, _batchCts.Token);
        BtnBatch.IsEnabled = true;
    }

    void BtnReset_Click(object s, RoutedEventArgs e) => _vm.ResetAdjustments();

    void BtnLoadLut_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = ".cube LUT 파일 선택",
            Filter = "CUBE LUT|*.cube|모든 파일|*.*",
        };
        if (dlg.ShowDialog() == true) _vm.AddCustomLut(dlg.FileName);
    }

    // ── LUT 목록 선택 ────────────────────────────────────────────────────

    void LutList_SelectionChanged(object s, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
    }

    // ── 보조 ─────────────────────────────────────────────────────────────

    void UpdateDropHint()
    {
        if (TxtDropHint != null)
            TxtDropHint.Visibility = _vm.HasImage ? Visibility.Collapsed : Visibility.Visible;
    }
}
