namespace AppTemp.Services;

public enum SandboxState { Idle, Running, Stopped }

public class SandboxService : IDisposable
{
    // ── 이벤트 ─────────────────────────────────────────────────────
    public event Action<ChangeRecord>? RecordAdded;
    public event Action<SandboxState>? StateChanged;
    public event Action?               ProcessExited;

    // ── 상태 ───────────────────────────────────────────────────────
    public SandboxState             State         { get; private set; } = SandboxState.Idle;
    public ObservableCollection<ChangeRecord> Records { get; } = [];
    public DateTime?                StartTime     { get; private set; }
    public DateTime?                EndTime       { get; private set; }
    public int                      FileChanges   => Records.Count(r => r.Category == ChangeCategory.File);
    public int                      RegChanges    => Records.Count(r => r.Category == ChangeCategory.Registry);

    private FileTracker?      _fileTracker;
    private RegistryTracker   _regTracker  = new();
    private RegistrySnapshot? _regBefore;
    private Process?          _process;
    private string?           _exePath;

    // ── 시작 ──────────────────────────────────────────────────────
    public async Task StartAsync(string exePath)
    {
        if (State == SandboxState.Running) return;

        _exePath = exePath;
        Records.Clear();
        StartTime = DateTime.Now;
        EndTime   = null;

        // 레지스트리 스냅샷 (백그라운드 — 다소 시간 걸림)
        _regBefore = await Task.Run(() => _regTracker.TakeSnapshot());

        // 파일 감시 시작
        _fileTracker = new FileTracker();
        _fileTracker.Changed += OnFileChanged;
        _fileTracker.Start();

        // 프로세스 실행
        try
        {
            _process = new Process
            {
                StartInfo           = new ProcessStartInfo(exePath) { UseShellExecute = true },
                EnableRaisingEvents = true
            };
            _process.Exited += OnProcessExited;
            _process.Start();
        }
        catch (Exception ex)
        {
            _fileTracker.Dispose();
            throw new InvalidOperationException($"프로세스 실행 실패: {ex.Message}", ex);
        }

        SetState(SandboxState.Running);
    }

    // ── 중지 ──────────────────────────────────────────────────────
    public async Task StopAsync()
    {
        if (State != SandboxState.Running) return;

        // 파일 감시 중지
        _fileTracker?.Stop();

        // 프로세스 종료 (이미 종료됐을 수도 있음)
        try { if (_process != null && !_process.HasExited) _process.Kill(entireProcessTree: true); }
        catch { }

        // 레지스트리 diff
        if (_regBefore != null)
        {
            var regAfter  = await Task.Run(() => _regTracker.TakeSnapshot());
            var regChanges = _regTracker.Diff(_regBefore, regAfter);
            foreach (var rec in regChanges)
                AddRecord(rec);
        }

        EndTime = DateTime.Now;
        SetState(SandboxState.Stopped);
    }

    // ── 롤백 ──────────────────────────────────────────────────────
    public async Task<(int filesRolledBack, int regRolledBack, List<string> errors)> RollbackAsync()
    {
        int filesOk = 0, regOk = 0;
        var errors  = new List<string>();

        await Task.Run(() =>
        {
            // 파일 롤백: Created → 삭제
            foreach (var rec in Records.Where(r => r.Category == ChangeCategory.File && r.Type == ChangeType.Created))
            {
                try
                {
                    if (File.Exists(rec.Path))  { File.Delete(rec.Path);            filesOk++; }
                    else if (Directory.Exists(rec.Path)) { Directory.Delete(rec.Path, true); filesOk++; }
                }
                catch (Exception ex) { errors.Add($"파일 삭제 실패: {rec.Path} ({ex.Message})"); }
            }

            // 레지스트리 롤백
            var regChanges = Records.Where(r => r.Category == ChangeCategory.Registry).ToList();
            try
            {
                _regTracker.Rollback(regChanges);
                regOk = regChanges.Count;
            }
            catch (Exception ex) { errors.Add($"레지스트리 롤백 실패: {ex.Message}"); }
        });

        return (filesOk, regOk, errors);
    }

    // ── 리포트 내보내기 ───────────────────────────────────────────
    public void ExportHtml(string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;background:#111;color:#ddd;padding:20px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}td,th{border:1px solid #333;padding:6px 10px;font-size:12px}");
        sb.AppendLine("th{background:#222}.created{color:#50dc78}.modified{color:#ff9040}.deleted{color:#ff5060}");
        sb.AppendLine("h1,h2{color:#3280ff}</style></head><body>");
        sb.AppendLine($"<h1>App.Temp 변경 리포트</h1>");
        sb.AppendLine($"<p>대상: <b>{_exePath}</b></p>");
        sb.AppendLine($"<p>시작: {StartTime:yyyy-MM-dd HH:mm:ss}  |  종료: {EndTime:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"<p>파일 변경: <b>{FileChanges}</b>건  |  레지스트리 변경: <b>{RegChanges}</b>건</p>");

        void WriteSection(string title, IEnumerable<ChangeRecord> records)
        {
            sb.AppendLine($"<h2>{title}</h2><table>");
            sb.AppendLine("<tr><th>시간</th><th>유형</th><th>경로</th><th>세부 정보</th></tr>");
            foreach (var r in records)
            {
                var cls  = r.Type == ChangeType.Created ? "created" : r.Type == ChangeType.Modified ? "modified" : "deleted";
                var detail = r.ValueName != null ? $"{r.ValueName}: {r.OldValue} → {r.NewValue}" : "";
                sb.AppendLine($"<tr class=\"{cls}\"><td>{r.TimestampStr}</td><td>{r.TypeLabel}</td>" +
                              $"<td>{EscHtml(r.DisplayPath)}</td><td>{EscHtml(detail)}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        WriteSection("파일 변경", Records.Where(r => r.Category == ChangeCategory.File));
        WriteSection("레지스트리 변경", Records.Where(r => r.Category == ChangeCategory.Registry));

        sb.AppendLine("</body></html>");
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    public void ExportCsv(string outputPath)
    {
        var lines = new List<string> { "시간,카테고리,유형,경로,값이름,이전값,새값" };
        foreach (var r in Records)
            lines.Add($"\"{r.TimestampStr}\",\"{r.CategoryLabel}\",\"{r.TypeLabel}\"," +
                      $"\"{r.DisplayPath}\",\"{r.ValueName}\",\"{r.OldValue}\",\"{r.NewValue}\"");
        File.WriteAllLines(outputPath, lines, Encoding.UTF8);
    }

    // ── Private helpers ───────────────────────────────────────────
    private void OnFileChanged(ChangeRecord rec) => AddRecord(rec);

    private void OnProcessExited(object? s, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // 마지막 파일 이벤트 수신 대기
            await StopAsync();
            ProcessExited?.Invoke();
        });
    }

    private void AddRecord(ChangeRecord rec)
    {
        Application.Current?.Dispatcher.Invoke(() => Records.Add(rec));
        RecordAdded?.Invoke(rec);
    }

    private void SetState(SandboxState s) { State = s; StateChanged?.Invoke(s); }

    private static string EscHtml(string? s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public void Dispose()
    {
        _fileTracker?.Dispose();
        _process?.Dispose();
    }
}
