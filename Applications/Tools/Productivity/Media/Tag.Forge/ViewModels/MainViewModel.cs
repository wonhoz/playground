namespace Tag.Forge.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    readonly TagService         _tags = new();
    readonly MusicBrainzService _mb   = new();

    public ObservableCollection<TrackViewModel> Tracks { get; } = new();

    string _status = "파일 또는 폴더를 끌어다 놓으세요";
    public string Status { get => _status; set { _status = value; Notify(); } }

    bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; Notify(); Notify(nameof(IsNotBusy)); } }
    public bool IsNotBusy => !_isBusy;

    // ── 파일 로드 ──────────────────────────────────────────────────────────

    public void LoadPaths(IEnumerable<string> paths)
    {
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))      files.AddRange(_tags.ScanFolder(p));
            else if (File.Exists(p))      files.Add(p);
        }
        var existing = new HashSet<string>(Tracks.Select(t => t.FilePath), StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var f in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existing.Contains(f)) continue;
            try { Tracks.Add(new TrackViewModel(_tags.Load(f))); added++; }
            catch { }
        }
        Status = $"트랙 {Tracks.Count}개" + (added > 0 ? $"  (+{added}개 추가)" : "");
    }

    // ── 저장 ──────────────────────────────────────────────────────────────

    public void SaveAll()
    {
        int saved = 0, failed = 0;
        foreach (var t in Tracks.Where(x => x.Modified).ToList())
        {
            try { _tags.Save(t.Info); t.Modified = false; saved++; }
            catch { failed++; }
        }
        Status = failed == 0
            ? $"저장 완료: {saved}개"
            : $"저장 완료: {saved}개  실패: {failed}개";
    }

    public void SaveSelected(IEnumerable<TrackViewModel> sel)
    {
        int saved = 0, failed = 0;
        foreach (var t in sel.ToList())
        {
            try { _tags.Save(t.Info); t.Modified = false; saved++; }
            catch { failed++; }
        }
        Status = $"저장 완료: {saved}개" + (failed > 0 ? $"  실패: {failed}개" : "");
    }

    // ── 제거 / 초기화 ──────────────────────────────────────────────────────

    public void RemoveSelected(IEnumerable<TrackViewModel> sel)
    {
        foreach (var t in sel.ToList()) Tracks.Remove(t);
        Status = $"트랙 {Tracks.Count}개";
    }

    public void ClearAll()
    {
        Tracks.Clear();
        Status = "파일 또는 폴더를 끌어다 놓으세요";
    }

    // ── 트랙 번호 자동 순번 ─────────────────────────────────────────────────

    public void AutoNumber(IEnumerable<TrackViewModel> sel)
    {
        int i = 1;
        foreach (var t in sel) { t.Track = (uint)i++; }
        Status = $"트랙 번호 {i - 1}개 자동 순번 적용";
    }

    // ── 파일명 ↔ 태그 동기화 ──────────────────────────────────────────────

    public int ApplyFilenameToTag(IEnumerable<TrackViewModel> sel, string pattern)
    {
        int applied = 0;
        foreach (var t in sel)
        {
            var r = FileNameParser.Parse(t.FileName, pattern);
            if (r == null) continue;
            if (r.TryGetValue("title",  out var v)) t.Title  = v;
            if (r.TryGetValue("artist", out v))     t.Artist = v;
            if (r.TryGetValue("album",  out v))     t.Album  = v;
            if (r.TryGetValue("year",   out v) && uint.TryParse(v, out var yr)) t.Year  = yr;
            if (r.TryGetValue("track",  out v) && uint.TryParse(v, out var tn)) t.Track = tn;
            applied++;
        }
        Status = $"파일명 → 태그: {applied}개 적용";
        return applied;
    }

    public int ApplyTagToFilename(IEnumerable<TrackViewModel> sel, string pattern)
    {
        int applied = 0;
        foreach (var t in sel)
        {
            var name = FileNameParser.Build(pattern, t.Info);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var ext     = Path.GetExtension(t.FilePath);
            var dir     = Path.GetDirectoryName(t.FilePath) ?? "";
            var newPath = Path.Combine(dir, name + ext);
            try
            {
                if (!t.FilePath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
                    File.Move(t.FilePath, newPath);
                t.Info.FilePath = newPath;
                t.Modified = true;
                applied++;
            }
            catch { }
        }
        Status = $"태그 → 파일명: {applied}개 적용";
        return applied;
    }

    // ── MusicBrainz 조회 ──────────────────────────────────────────────────

    CancellationTokenSource? _mbCts;

    public async Task LookupMusicBrainzAsync(IEnumerable<TrackViewModel> sel)
    {
        _mbCts?.Cancel();
        _mbCts = new CancellationTokenSource();
        var ct = _mbCts.Token;
        IsBusy = true;
        int found = 0;
        foreach (var t in sel.ToList())
        {
            if (ct.IsCancellationRequested) break;
            Status = $"MusicBrainz 조회 중: {t.FileName}";
            var r = await _mb.LookupAsync(t.Title, t.Artist, ct);
            if (r != null)
            {
                if (!string.IsNullOrEmpty(r.Title))  t.Title  = r.Title;
                if (!string.IsNullOrEmpty(r.Artist)) t.Artist = r.Artist;
                if (!string.IsNullOrEmpty(r.Album))  t.Album  = r.Album;
                if (r.Year > 0)                      t.Year   = r.Year;
                found++;
            }
            try { await Task.Delay(1100, ct); } catch (OperationCanceledException) { break; }
        }
        IsBusy = false;
        Status = ct.IsCancellationRequested
            ? "MusicBrainz 조회 취소됨"
            : $"MusicBrainz 조회 완료: {found}개 업데이트";
    }

    public void CancelMusicBrainz() => _mbCts?.Cancel();
}
