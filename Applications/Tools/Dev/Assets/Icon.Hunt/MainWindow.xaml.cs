using System.IO;
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
    private CancellationTokenSource? _thumbCts;

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

        // 검색 결과 변경 시 썸네일 로딩
        _vm.Icons.CollectionChanged += (_, _) => _ = LoadThumbnailsAsync();
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

    // ── SVG 상세 로드 (색상 적용) ────────────────────────────────
    private async Task LoadDetailSvgAsync(IconEntry icon)
    {
        DetailSvgImage.Source = null;
        TblDetailLoading.Visibility = Visibility.Visible;

        try
        {
            // ViewModel의 LoadSelectedSvgAsync 완료 대기 (최대 2초)
            for (int i = 0; i < 20 && _vm.SelectedSvgContent == null; i++)
                await Task.Delay(100);

            await RenderDetailWithColorAsync();
        }
        finally
        {
            TblDetailLoading.Visibility = Visibility.Collapsed;
        }
    }

    // 현재 미리보기 색상으로 상세 SVG 재렌더링
    private async Task RenderDetailWithColorAsync()
    {
        var content = _vm.SelectedSvgContent;
        if (content == null) return;
        var fg = _vm.PreviewFg;
        var colored = IconifyService.ApplyColor(content, fg);
        var img = await Task.Run(() => SvgRenderService.RenderString(colored));
        if (img != null) DetailSvgImage.Source = img;
    }

    // ── 그리드 썸네일 로더 ────────────────────────────────────
    // 검색 결과를 순차적으로 하나씩 다운로드 + 렌더링
    // - 다운로드: await (비동기, UI 스레드 해방)
    // - 렌더링: Dispatcher Background 우선순위 (입력 이벤트보다 낮음)
    // - Task.Run 사용 금지 (SharpVectors가 MTA 스레드에서 크래시)
    private async Task LoadThumbnailsAsync()
    {
        _thumbCts?.Cancel();
        _thumbCts = new CancellationTokenSource();
        var ct = _thumbCts.Token;

        var icons = _vm.Icons.ToList();

        foreach (var icon in icons)
        {
            if (ct.IsCancellationRequested) return;
            if (icon.Thumbnail != null) continue; // 이미 로딩된 항목 스킵

            try
            {
                // 1단계: SVG 다운로드/캐시 (네트워크 I/O — UI 스레드 해방)
                var path = await _vm.GetSvgPathAsync(icon, ct);
                if (path == null || ct.IsCancellationRequested) continue;

                var content = await File.ReadAllTextAsync(path, ct);
                var colored = IconifyService.ApplyColor(content, "#C8C8DC");

                // 2단계: 렌더링 (UI 스레드, Background 우선순위)
                await Dispatcher.InvokeAsync(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        var img = SvgRenderService.RenderString(colored);
                        if (img != null) icon.Thumbnail = img;
                    }
                    catch { }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (OperationCanceledException) { return; }
            catch { }
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

        var errors = new List<string>();

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

                var err = await _vm.IndexCollectionAsync(col, progress, ct);
                if (err != null) errors.Add(err);
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

        // 인덱싱 실패 시 사용자에게 알림
        if (!ct.IsCancellationRequested && errors.Count > 0 && _vm.TotalCount == 0)
        {
            var msg = string.Join("\n", errors.Take(5));
            if (errors.Count > 5) msg += $"\n... 외 {errors.Count - 5}개";
            System.Windows.MessageBox.Show(
                $"인덱싱 중 오류가 발생했습니다.\n네트워크 연결을 확인하고 다시 시도하세요.\n\n{msg}",
                "인덱싱 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (!ct.IsCancellationRequested && errors.Count > 0)
        {
            _vm.StatusText = $"일부 실패 ({errors.Count}개): {errors[0]}";
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
        // 배경 전환 후 SVG 색상 재렌더링
        _ = RenderDetailWithColorAsync();
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
