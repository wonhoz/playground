using Microsoft.Win32;
using ZipPeek.Services;

namespace ZipPeek.Views;

public partial class ArchiveView : UserControl
{
    private readonly ArchiveService _archSvc = new();
    private readonly PreviewService _previewSvc = new();
    private readonly SearchService _searchSvc = new();

    private readonly string _archivePath;
    private readonly MainWindow _main;

    private CancellationTokenSource? _searchCts;

    public ArchiveView(string archivePath, MainWindow main)
    {
        InitializeComponent();
        _archivePath = archivePath;
        _main = main;
        _ = LoadArchiveAsync();
    }

    // ── 아카이브 로드 ─────────────────────────────────────────────
    private async Task LoadArchiveAsync()
    {
        _main.SetStatus("아카이브 로드 중...");
        _main.ShowProgress(true, true);
        try
        {
            var (roots, stats) = await Task.Run(() => _archSvc.Open(_archivePath));
            foreach (var node in roots)
                ArchiveTree.Items.Add(node);

            TxtEntryCount.Text = $"  {stats.TotalFiles}개 파일  {stats.TotalDirs}개 폴더";
            _main.SetStats($"{stats.Format}  •  {FormatSize(stats.TotalUncompressed)}  →  {FormatSize(stats.TotalCompressed)}");
            _main.SetStatus("준비");
        }
        catch (Exception ex)
        {
            _main.SetStatus($"오류: {ex.Message}");
        }
        finally
        {
            _main.ShowProgress(false);
        }
    }

    // ── 트리 선택 → 미리보기 ──────────────────────────────────────
    private async void ArchiveTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not ArchiveNode node || node.IsDirectory) return;

        TxtPreviewTitle.Text = node.Name;
        TxtPreviewMeta.Text = node.SizeText;
        _main.SetStatus($"로드 중: {node.FullPath}");
        _main.ShowProgress(true, true);

        try
        {
            var data = await Task.Run(() => _archSvc.ReadEntry(_archivePath, node.FullPath));
            var result = _previewSvc.Preview(data, node.Name);

            TxtPreview.Text = "";
            ImgPreview.Source = null;
            TxtHex.Text = "";

            switch (result.Kind)
            {
                case PreviewKind.Text:
                    TxtPreview.Text = result.Text;
                    TxtPreviewMeta.Text = $"{node.SizeText}  •  {result.Encoding}";
                    PreviewTabs.SelectedItem = TabText;
                    break;
                case PreviewKind.Image:
                    ImgPreview.Source = result.Image;
                    PreviewTabs.SelectedItem = TabImage;
                    break;
                case PreviewKind.Hex:
                case PreviewKind.Binary:
                    TxtHex.Text = await Task.Run(() => PreviewService.ToHexDump(data));
                    PreviewTabs.SelectedItem = TabHex;
                    break;
            }
            _main.SetStatus("준비");
        }
        catch (Exception ex)
        {
            TxtPreview.Text = $"미리보기 실패: {ex.Message}";
            PreviewTabs.SelectedItem = TabText;
            _main.SetStatus($"오류: {ex.Message}");
        }
        finally
        {
            _main.ShowProgress(false);
        }
    }

    // ── 검색 ─────────────────────────────────────────────────────
    public async void RunSearch(string query, bool contentSearch)
    {
        if (string.IsNullOrWhiteSpace(query)) { ClearSearch(); return; }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        PreviewTabs.SelectedItem = TabSearch;
        SearchResultList.Items.Clear();
        TxtSearchSummary.Text = "검색 중...";
        _main.ShowProgress(true, true);
        _main.SetStatus($"'{query}' 검색 중...");

        try
        {
            var results = await _searchSvc.SearchAsync(
                _archivePath, query, contentSearch,
                null, _searchCts.Token);

            SearchResultList.Items.Clear();
            foreach (var r in results)
                SearchResultList.Items.Add(r);

            TxtSearchSummary.Text = results.Count > 0
                ? $"'{query}' — {results.Count}건 발견"
                : $"'{query}' — 결과 없음";
            _main.SetStatus($"검색 완료: {results.Count}건");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            TxtSearchSummary.Text = $"검색 오류: {ex.Message}";
        }
        finally
        {
            _main.ShowProgress(false);
        }
    }

    public void ClearSearch()
    {
        _searchCts?.Cancel();
        SearchResultList.Items.Clear();
        TxtSearchSummary.Text = "";
    }

    private async void SearchResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultList.SelectedItem is not SearchResult result) return;

        // 해당 파일 미리보기
        TxtPreviewTitle.Text = Path.GetFileName(result.FileName);
        _main.ShowProgress(true, true);
        try
        {
            var data = await Task.Run(() => _archSvc.ReadEntry(_archivePath, result.EntryKey));
            var preview = _previewSvc.Preview(data, result.FileName);

            if (preview.Kind == PreviewKind.Text && preview.Text != null)
            {
                TxtPreview.Text = preview.Text;
                TxtPreviewMeta.Text = $"{FormatSize(preview.FileSize)}  •  {preview.Encoding}";
                // 해당 줄로 스크롤 (근사)
                if (result.Line > 0)
                {
                    var lines = preview.Text.Split('\n');
                    int charPos = lines.Take(result.Line - 1).Sum(l => l.Length + 1);
                    TxtPreview.CaretIndex = Math.Min(charPos, TxtPreview.Text.Length);
                    TxtPreview.ScrollToLine(result.Line - 1);
                }
                PreviewTabs.SelectedItem = TabText;
            }
        }
        catch { }
        finally { _main.ShowProgress(false); }
    }

    // ── 추출 ─────────────────────────────────────────────────────
    public async void ExtractSelected()
    {
        if (ArchiveTree.SelectedItem is not ArchiveNode node) return;
        var dir = PickFolder(); if (dir is null) return;

        _main.ShowProgress(true, false);
        var prog = new Progress<(int d, int t)>(x =>
        {
            _main.SetProgress(x.t > 0 ? x.d * 100.0 / x.t : 0);
            _main.SetStatus($"추출 중... {x.d}/{x.t}");
        });
        try
        {
            await _archSvc.ExtractNodesAsync(_archivePath, [node], dir, prog);
            _main.SetStatus($"추출 완료: {dir}");
        }
        catch (Exception ex) { _main.SetStatus($"추출 실패: {ex.Message}"); }
        finally { _main.ShowProgress(false); }
    }

    public async void ExtractAll()
    {
        var dir = PickFolder(); if (dir is null) return;

        _main.ShowProgress(true, false);
        var prog = new Progress<(int d, int t)>(x =>
        {
            _main.SetProgress(x.t > 0 ? x.d * 100.0 / x.t : 0);
            _main.SetStatus($"추출 중... {x.d}/{x.t}");
        });
        try
        {
            await _archSvc.ExtractAllAsync(_archivePath, dir, prog);
            _main.SetStatus($"전체 추출 완료: {dir}");
            MessageBox.Show($"추출 완료!\n{dir}", "Zip.Peek", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { _main.SetStatus($"추출 실패: {ex.Message}"); }
        finally { _main.ShowProgress(false); }
    }

    private static string? PickFolder()
    {
        var dlg = new OpenFolderDialog { Title = "추출 폴더 선택" };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private static string FormatSize(long b) => b switch
    {
        >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{b / 1_048_576.0:F1} MB",
        >= 1_024 => $"{b / 1024.0:F0} KB",
        _ => $"{b} B"
    };
}
