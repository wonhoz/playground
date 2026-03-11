using Microsoft.Win32;

namespace CrashView;

public partial class MainWindow : Window
{
    private DumpInfo? _current;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            SetStatus("덤프 파일을 열거나 드래그하여 분석하세요.");
        };

        // 드래그&드롭 등록
        Drop     += MainWindow_Drop;
        DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // 파일 열기
    // ──────────────────────────────────────────────────────────────────

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "덤프 파일 선택",
            Filter = "덤프 파일|*.dmp;*.mdmp|모든 파일|*.*"
        };
        if (dlg.ShowDialog(this) == true) _ = LoadDumpAsync(dlg.FileName);
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        var dump = files.FirstOrDefault(f =>
            f.EndsWith(".dmp",  StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".mdmp", StringComparison.OrdinalIgnoreCase));
        if (dump != null) _ = LoadDumpAsync(dump);
    }

    // ──────────────────────────────────────────────────────────────────
    // 분석
    // ──────────────────────────────────────────────────────────────────

    private async Task LoadDumpAsync(string path)
    {
        SetStatus($"분석 중: {Path.GetFileName(path)}");
        SetBusy(true);
        TxtAnalysisLog.Text = "";
        BtnExport.IsEnabled = false;

        var log = new System.Text.StringBuilder();
        var progress = new Progress<string>(msg =>
        {
            log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            TxtAnalysisLog.Text = log.ToString();
            SetStatus(msg);
        });

        try
        {
            _current = await DumpAnalyzer.AnalyzeAsync(path, progress);
            UpdateUI();
            BtnExport.IsEnabled = true;
            SetStatus($"분석 완료 — {_current.Modules.Count}개 모듈, {_current.Threads.Count}개 스레드");
        }
        catch (Exception ex)
        {
            log.AppendLine($"[오류] {ex.Message}");
            TxtAnalysisLog.Text = log.ToString();
            SetStatus($"분석 실패: {ex.Message}");
        }
        finally { SetBusy(false); }
    }

    // ──────────────────────────────────────────────────────────────────
    // UI 업데이트
    // ──────────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (_current == null) return;

        // 예외 요약
        TxtExType.Text = string.IsNullOrEmpty(_current.ExceptionType) ? "(예외 없음)" : _current.ExceptionType;
        TxtExCode.Text = _current.ExceptionCode != 0 ? _current.ExceptionCodeName : "";
        TxtExMsg.Text  = string.IsNullOrEmpty(_current.ExceptionMessage) ? "" : _current.ExceptionMessage;
        TxtArch.Text   = _current.Architecture;
        TxtManaged.Text = _current.IsManaged ? "예 (.NET)" : "아니오";

        // GC 힙
        if (_current.IsManaged && _current.HeapSize > 0)
        {
            double max = _current.HeapSize;
            PbGen0.Maximum = max; PbGen0.Value = _current.Gen0Size;
            PbGen1.Maximum = max; PbGen1.Value = _current.Gen1Size;
            PbGen2.Maximum = max; PbGen2.Value = _current.Gen2Size;
            PbLoh.Maximum  = max; PbLoh.Value  = _current.LohSize;
            TxtGen0.Text = FormatBytes(_current.Gen0Size);
            TxtGen1.Text = FormatBytes(_current.Gen1Size);
            TxtGen2.Text = FormatBytes(_current.Gen2Size);
            TxtLoh.Text  = FormatBytes(_current.LohSize);
        }

        // 콜스택
        LstStack.ItemsSource = _current.CrashStack;
        TxtStackHeader.Text = $"크래시 스레드 콜스택 ({_current.CrashStack.Count}개 프레임)";

        // 모듈
        LstModules.ItemsSource = _current.Modules;
        TxtModuleHeader.Text = $"로드된 모듈 ({_current.Modules.Count}개)";

        // 스레드
        LstThreads.ItemsSource = _current.Threads;
        var crashThread = _current.Threads.FirstOrDefault(t => t.IsCrash);
        if (crashThread != null) LstThreads.SelectedItem = crashThread;
    }

    private void LstThreads_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (LstThreads.SelectedItem is ThreadInfo thread)
        {
            LstStack.ItemsSource = thread.Stack;
            TxtStackHeader.Text = $"TID {thread.ThreadId} 콜스택 ({thread.Stack.Count}개 프레임)";
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // 리포트 내보내기
    // ──────────────────────────────────────────────────────────────────

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var dlg = new SaveFileDialog
        {
            Title      = "리포트 저장",
            Filter     = "Markdown|*.md|HTML|*.html",
            FileName   = Path.GetFileNameWithoutExtension(_current.FileName) + "_crash_report"
        };
        if (dlg.ShowDialog(this) != true) return;

        var md = DumpAnalyzer.ExportMarkdown(_current);
        if (dlg.FilterIndex == 2)
        {
            // 간단 HTML 변환
            var escaped = md.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var html = $"<html><head><meta charset='utf-8'><style>body{{background:#1a1a1a;color:#e0e0e0;font-family:Consolas,monospace;padding:20px}}</style></head><body><pre>{escaped}</pre></body></html>";
            File.WriteAllText(dlg.FileName, html, System.Text.Encoding.UTF8);
        }
        else
        {
            File.WriteAllText(dlg.FileName, md, System.Text.Encoding.UTF8);
        }
        SetStatus($"리포트 저장 완료: {dlg.FileName}");
    }

    // ──────────────────────────────────────────────────────────────────
    // 유틸
    // ──────────────────────────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        PbProgress.IsIndeterminate = busy;
        PbProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        BtnOpen.IsEnabled = !busy;
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024}K";
        return $"{bytes / 1024 / 1024}M";
    }
}
