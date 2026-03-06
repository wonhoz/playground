using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IconHunt.Models;
using IconHunt.Services;
using IconHunt.ViewModels;
using Microsoft.Win32;

namespace IconHunt;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private CancellationTokenSource? _indexCts;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow()
    {
        _vm = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();

        Loaded += OnLoaded;
        Closed += (_, _) => _vm.Dispose();

        // 아이콘 카드 클릭 이벤트 연결
        IconGrid.PreviewMouseLeftButtonUp += IconGrid_ItemClick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }

    // ── 아이콘 카드 클릭 ────────────────────────────────────────
    private void IconGrid_ItemClick(object sender, MouseButtonEventArgs e)
    {
        var hit = VisualTreeHelper.HitTest(IconGrid, e.GetPosition(IconGrid));
        if (hit == null) return;

        var dep = hit.VisualHit as DependencyObject;
        while (dep != null)
        {
            if (dep is FrameworkElement fe && fe.DataContext is IconEntry icon)
            {
                _vm.SelectedIcon = icon;
                _ = LoadDetailSvgAsync(icon);
                return;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }
    }

    // ── SVG 상세 로드 ────────────────────────────────────────────
    private async Task LoadDetailSvgAsync(IconEntry icon)
    {
        DetailSvgImage.Source = null;
        TblDetailLoading.Visibility = Visibility.Visible;

        try
        {
            if (_vm.SelectedSvgPath != null)
            {
                var img = await Task.Run(() => SvgRenderService.RenderFile(_vm.SelectedSvgPath));
                if (img != null)
                {
                    DetailSvgImage.Source = img;
                    return;
                }
            }

            // SVG 로딩 대기 (ViewModel에서 비동기 로드 중)
            await Task.Delay(300);
            if (_vm.SelectedSvgPath != null)
            {
                var img = await Task.Run(() => SvgRenderService.RenderFile(_vm.SelectedSvgPath));
                if (img != null)
                    DetailSvgImage.Source = img;
            }
        }
        finally
        {
            TblDetailLoading.Visibility = Visibility.Collapsed;
        }
    }

    // ── 라이브러리 인덱싱 ────────────────────────────────────────
    private async void BtnIndex_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconCollection col)
            await RunIndexAsync(new[] { col });
    }

    private async void BtnIndexAll_Click(object sender, RoutedEventArgs e)
    {
        var cols = _vm.Collections.ToList();
        await RunIndexAsync(cols);
    }

    private async Task RunIndexAsync(IEnumerable<IconCollection> collections)
    {
        _indexCts = new CancellationTokenSource();
        var ct = _indexCts.Token;

        IndexProgressPanel.Visibility = Visibility.Visible;
        _vm.IsLoading = true;

        try
        {
            var colList = collections.ToList();
            int colIdx = 0;

            foreach (var col in colList)
            {
                if (ct.IsCancellationRequested) break;
                colIdx++;

                var progress = new Progress<(int done, int total, string status)>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TblIndexStatus.Text = $"[{colIdx}/{colList.Count}] {col.Name}";
                        TblIndexDetail.Text = p.status;
                        PbIndexProgress.Maximum = p.total > 0 ? p.total : 1;
                        PbIndexProgress.Value = p.done;
                    });
                });

                await _vm.IndexCollectionAsync(col, progress, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _vm.StatusText = "인덱싱 취소됨";
        }
        finally
        {
            _vm.IsLoading = false;
            IndexProgressPanel.Visibility = Visibility.Collapsed;
            _indexCts = null;
        }
    }

    private void BtnCancelIndex_Click(object sender, RoutedEventArgs e)
    {
        _indexCts?.Cancel();
    }

    // ── 미리보기 토글 ────────────────────────────────────────────
    private void BtnTogglePreview_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsDarkPreview = !_vm.IsDarkPreview;
    }

    // ── 즐겨찾기 ─────────────────────────────────────────────────
    private void BtnFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedIcon != null)
            _vm.ToggleFavorite(_vm.SelectedIcon);
    }

    // ── 클립보드 복사 ────────────────────────────────────────────
    private void BtnCopySvg_Click(object sender, RoutedEventArgs e) => _vm.CopySvg();
    private void BtnCopyName_Click(object sender, RoutedEventArgs e) => _vm.CopyName();
    private void BtnCopyId_Click(object sender, RoutedEventArgs e) => _vm.CopyId();

    // ── PNG 저장 ──────────────────────────────────────────────────
    private async void BtnSavePng_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedIcon == null) return;

        var dlg = new SaveFileDialog
        {
            Title = "PNG로 저장",
            Filter = "PNG 파일|*.png",
            FileName = $"{_vm.SelectedIcon.Id.Replace(":", "_")}.png",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() != true) return;

        // 크기 선택
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256, 512 };
        var menu = new ContextMenu { Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x30)) };
        foreach (var sz in sizes)
        {
            var item = new MenuItem
            {
                Header = $"{sz} × {sz} px",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x30)),
                Tag = (sz, dlg.FileName)
            };
            item.Click += async (s2, _) =>
            {
                var (size, path) = ((int, string))((MenuItem)s2!).Tag;
                await _vm.SaveAsPngAsync(path, size);
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    // ── 캐시 정리 ─────────────────────────────────────────────────
    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "다운로드된 SVG 파일을 모두 삭제하시겠습니까?\n다음 사용 시 다시 다운로드됩니다.",
            "캐시 정리", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        var bytes = IconifyService.GetCacheSizeBytes();
        IconifyService.ClearCache();
        _vm.StatusText = $"캐시 정리 완료 ({bytes / 1024.0:F1} KB 삭제)";
    }
}
