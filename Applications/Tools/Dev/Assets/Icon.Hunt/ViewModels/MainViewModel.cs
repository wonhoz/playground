using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using IconHunt.Models;
using IconHunt.Services;

namespace IconHunt.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IconDatabase _db;
    private readonly IconifyService _iconify;

    public ObservableCollection<IconEntry> Icons { get; } = new();
    public ObservableCollection<IconCollection> Collections { get; } = new();
    public ObservableCollection<IconEntry> Recents { get; } = new();
    public ObservableCollection<IconEntry> Favorites { get; } = new();

    private HashSet<string> _favoriteIds = new();
    private CancellationTokenSource? _searchCts;

    // ── 바인딩 프로퍼티 ─────────────────────────────────────
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(nameof(SearchText)); _ = SearchAsync(); }
    }

    private IconEntry? _selectedIcon;
    public IconEntry? SelectedIcon
    {
        get => _selectedIcon;
        set
        {
            _selectedIcon = value;
            OnPropertyChanged(nameof(SelectedIcon));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedCollectionInfo));
            if (value != null)
            {
                _db.AddRecent(value.Id);
                RefreshRecents();
                _ = LoadSelectedSvgAsync(value);
            }
        }
    }

    public bool HasSelection => _selectedIcon != null;

    private string? _selectedSvgContent;
    public string? SelectedSvgContent
    {
        get => _selectedSvgContent;
        set { _selectedSvgContent = value; OnPropertyChanged(nameof(SelectedSvgContent)); }
    }

    private string? _selectedSvgPath;
    public string? SelectedSvgPath
    {
        get => _selectedSvgPath;
        set { _selectedSvgPath = value; OnPropertyChanged(nameof(SelectedSvgPath)); }
    }

    private bool _isDarkPreview = true;
    public bool IsDarkPreview
    {
        get => _isDarkPreview;
        set { _isDarkPreview = value; OnPropertyChanged(nameof(IsDarkPreview)); OnPropertyChanged(nameof(PreviewBg)); OnPropertyChanged(nameof(PreviewFg)); }
    }

    public string PreviewBg => _isDarkPreview ? "#1A1A2E" : "#F8F8F8";
    public string PreviewFg => _isDarkPreview ? "#E0E0E0" : "#1A1A1A";

    private string _statusText = "준비";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; OnPropertyChanged(nameof(TotalCount)); OnPropertyChanged(nameof(StatsText)); }
    }

    private int _resultCount;
    public int ResultCount
    {
        get => _resultCount;
        set { _resultCount = value; OnPropertyChanged(nameof(ResultCount)); OnPropertyChanged(nameof(StatsText)); }
    }

    public string StatsText =>
        $"총 {TotalCount:N0}개 인덱스 | 검색 결과: {ResultCount:N0}개";

    private bool _showFavoritesOnly;
    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set { _showFavoritesOnly = value; OnPropertyChanged(nameof(ShowFavoritesOnly)); _ = SearchAsync(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
    }

    public string? SelectedCollectionInfo
    {
        get
        {
            if (_selectedIcon == null) return null;
            var col = Collections.FirstOrDefault(c => c.Prefix == _selectedIcon.Prefix);
            return col == null ? _selectedIcon.Prefix : $"{col.Name}  •  {col.License}  •  {col.Author}";
        }
    }

    public MainViewModel()
    {
        _db = new IconDatabase();
        _iconify = new IconifyService();
        InitCollections();
        RefreshRecents();
        RefreshFavorites();
        TotalCount = _db.CountIcons();
        _ = SearchAsync();
    }

    private void InitCollections()
    {
        var dbCols = _db.GetCollections().ToDictionary(c => c.Prefix);
        foreach (var col in IconCollection.DefaultCollections)
        {
            if (dbCols.TryGetValue(col.Prefix, out var dbCol))
            {
                col.IsIndexed = dbCol.IsIndexed;
                col.Total = dbCol.Total > 0 ? dbCol.Total : col.Total;
            }
            else
            {
                _db.UpsertCollection(col);
            }
            col.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IconCollection.IsEnabled))
                    _ = SearchAsync();
            };
            Collections.Add(col);
        }
    }

    // ── 검색 ────────────────────────────────────────────────
    public async Task SearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        try
        {
            await Task.Delay(200, ct); // 디바운스

            var activePrefixes = Collections
                .Where(c => c.IsEnabled && c.IsIndexed)
                .Select(c => c.Prefix)
                .ToList();

            List<IconEntry> results;
            if (_showFavoritesOnly)
            {
                results = _db.GetFavorites(200);
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    var q = _searchText.ToLowerInvariant();
                    results = results.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                                 r.Tags.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }
            else
            {
                results = await Task.Run(() =>
                    _db.Search(_searchText, activePrefixes.Count > 0 ? activePrefixes : null, 120), ct);
            }

            if (ct.IsCancellationRequested) return;

            // 즐겨찾기 플래그 적용
            foreach (var icon in results)
            {
                icon.IsFavorite = _favoriteIds.Contains(icon.Id);
                var col = Collections.FirstOrDefault(c => c.Prefix == icon.Prefix);
                if (col != null)
                {
                    icon.CollectionName = col.Name;
                    icon.License = col.License;
                }
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                Icons.Clear();
                foreach (var icon in results) Icons.Add(icon);
                ResultCount = results.Count;
                StatusText = results.Count == 0 ? "검색 결과 없음" : $"{results.Count}개 표시 중";
            });
        }
        catch (OperationCanceledException) { }
    }

    // ── 아이콘 라이브러리 인덱싱 ────────────────────────────
    // 반환값: null = 성공, 에러 메시지 문자열 = 실패
    public async Task<string?> IndexCollectionAsync(IconCollection col,
        IProgress<(int done, int total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report((0, 1, $"{col.Name} 다운로드 중..."));
        StatusText = $"{col.Name} 인덱싱 중...";

        List<IconEntry> icons;
        try
        {
            icons = await _iconify.FetchCollectionIconsAsync(col.Prefix,
                new Progress<(int, int)>(p => progress?.Report((p.Item1, p.Item2, $"{p.Item1}/{p.Item2}"))),
                ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            StatusText = $"{col.Name} 다운로드 실패: {ex.Message}";
            return $"{col.Name}: {ex.Message}";
        }

        if (icons.Count == 0)
        {
            StatusText = $"{col.Name} 다운로드 실패 (아이콘 없음)";
            return $"{col.Name}: 아이콘을 가져오지 못했습니다";
        }

        try
        {
            await Task.Run(() => _db.BulkInsertIcons(icons), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            StatusText = $"{col.Name} DB 저장 실패";
            return $"{col.Name} DB 저장 오류: {ex.Message}";
        }

        _db.MarkCollectionIndexed(col.Prefix, icons.Count);
        col.IsIndexed = true;
        col.Total = icons.Count;
        _db.UpsertCollection(col);

        TotalCount = _db.CountIcons();
        progress?.Report((icons.Count, icons.Count, "완료"));
        StatusText = $"{col.Name} 인덱싱 완료 ({icons.Count:N0}개)";
        await SearchAsync();
        return null;
    }

    // 썸네일용: SVG 파일 경로 반환 (캐시 우선, 없으면 다운로드)
    public Task<string?> GetSvgPathAsync(IconEntry icon, CancellationToken ct = default)
        => _iconify.GetSvgPathAsync(icon.Prefix, icon.Name, ct);

    // ── SVG 로드 ─────────────────────────────────────────────
    private async Task LoadSelectedSvgAsync(IconEntry icon)
    {
        SelectedSvgContent = null;
        SelectedSvgPath = null;

        var path = await _iconify.GetSvgPathAsync(icon.Prefix, icon.Name);
        if (path != null)
        {
            SelectedSvgPath = path;
            SelectedSvgContent = await System.IO.File.ReadAllTextAsync(path);
        }
        else
        {
            StatusText = $"{icon.Id} SVG 로드 실패 (오프라인?)";
        }
    }

    // ── 즐겨찾기 ────────────────────────────────────────────
    public void ToggleFavorite(IconEntry icon)
    {
        icon.IsFavorite = !icon.IsFavorite;
        _db.SetFavorite(icon.Id, icon.IsFavorite);
        if (icon.IsFavorite) _favoriteIds.Add(icon.Id);
        else _favoriteIds.Remove(icon.Id);
        RefreshFavorites();
    }

    private void RefreshFavorites()
    {
        _favoriteIds = _db.GetFavoriteIds();
        var favs = _db.GetFavorites(50);
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Favorites.Clear();
            foreach (var f in favs) Favorites.Add(f);
        });
    }

    private void RefreshRecents()
    {
        var recents = _db.GetRecents(10);
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Recents.Clear();
            foreach (var r in recents) Recents.Add(r);
        });
    }

    // ── 클립보드 복사 ────────────────────────────────────────
    public void CopySvg()
    {
        if (_selectedSvgContent != null)
        {
            Clipboard.SetText(_selectedSvgContent);
            StatusText = $"{_selectedIcon?.Id} SVG 복사 완료";
        }
    }

    public void CopyName()
    {
        if (_selectedIcon != null)
        {
            Clipboard.SetText(_selectedIcon.Name);
            StatusText = $"이름 '{_selectedIcon.Name}' 복사 완료";
        }
    }

    public void CopyId()
    {
        if (_selectedIcon != null)
        {
            Clipboard.SetText(_selectedIcon.Id);
            StatusText = $"ID '{_selectedIcon.Id}' 복사 완료";
        }
    }

    public async Task SaveAsPngAsync(string outputPath, int size)
    {
        if (_selectedSvgContent == null) return;
        var content = _selectedSvgContent;
        var ok = await Task.Run(() => SvgRenderService.SaveAsPng(content, outputPath, size));
        StatusText = ok ? $"PNG 저장 완료: {System.IO.Path.GetFileName(outputPath)}"
                        : "PNG 저장 실패";
    }

    public void Dispose()
    {
        _db.Dispose();
        _iconify.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
