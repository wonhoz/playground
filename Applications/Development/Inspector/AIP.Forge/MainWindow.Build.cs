using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AIPForge.Services;
using AIPForge.Views;

namespace AIPForge;

/// <summary>AdvancedInstaller.com 빌드 실행 · 로그 패널</summary>
public partial class MainWindow
{
    private enum LogKind { Normal, Header, Info, Success, Warn, Error }

    private const string AllBuilds = "(모든 빌드)";
    private const int MaxLogLines = 20000;

    private CancellationTokenSource? _buildCts;
    private bool _building;
    private string? _lastOutputPath;

    private static readonly Regex ErrorRegex = new(@"\b(error|failed|failure|cannot|could not)\b|오류|실패", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WarnRegex = new(@"\bwarning\b|경고", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void PopulateBuildCombo()
    {
        CmbBuild.Items.Clear();
        var builds = _doc?.Builds().Select(b => b.Name).ToList() ?? [];
        if (builds.Count > 1) CmbBuild.Items.Add(AllBuilds);
        foreach (var b in builds) CmbBuild.Items.Add(b);
        if (CmbBuild.Items.Count > 0) CmbBuild.SelectedIndex = 0;
        CmbBuild.Visibility = builds.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string? SelectedBuildName()
        => CmbBuild.SelectedItem is string s && s != AllBuilds && CmbBuild.Items.Count > 1 ? s : null;

    private async void BtnBuild_Click(object sender, RoutedEventArgs e) => await BuildCurrentAsync(rebuild: false, SelectedBuildName());

    private async void BtnRebuild_Click(object sender, RoutedEventArgs e) => await BuildCurrentAsync(rebuild: true, SelectedBuildName());

    private void ChkSyncBeforeBuild_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.RefreshSyncBeforeBuild = ChkSyncBeforeBuild.IsChecked == true;
        _settings.Save();
    }

    /// <summary>현재 파일 빌드 — 저장 안 된 변경이 있으면 먼저 저장</summary>
    private async Task BuildCurrentAsync(bool rebuild, string? buildName)
    {
        if (_doc?.FilePath == null || _building || !EnsureCli()) return;
        if (!CommitPendingEdits()) return;
        if (_dirty)
        {
            if (!DarkDialog.Confirm(this, "빌드는 디스크에 저장된 파일로 진행됩니다.\n변경 사항을 저장하고 빌드할까요?", "저장 후 빌드"))
                return;
            if (!SaveDocument()) return;
        }

        ShowLog(clear: true);
        await RunBuildAsync([_doc.FilePath], rebuild, buildName);
        await ReloadIfChangedOnDiskAsync();
    }

    /// <summary>파일 목록을 차례로 빌드. (성공 수, 실패 수) 반환</summary>
    private async Task<(int ok, int fail)> RunBuildAsync(IReadOnlyList<string> files, bool rebuild, string? buildName)
    {
        _building = true;
        _buildCts = new CancellationTokenSource();
        var ct = _buildCts.Token;
        _lastOutputPath = null;
        BtnOpenOutput.IsEnabled = false;
        UpdateUiState();
        if (_current?.Kind == NavKind.Overview) RenderOverview();

        var sw = Stopwatch.StartNew();
        int ok = 0, fail = 0;
        SetBuildState("빌드 중...", "AccentBrush");
        try
        {
            for (var i = 0; i < files.Count && !ct.IsCancellationRequested; i++)
            {
                var file = files[i];
                AppendLog($"━━ [{i + 1}/{files.Count}] {Path.GetFileName(file)}  {(rebuild ? "(리빌드)" : "")}{(buildName != null ? $"  구성: {buildName}" : "")}", LogKind.Header);
                SetBuildState($"빌드 중... ({i + 1}/{files.Count}) {Path.GetFileName(file)}", "AccentBrush");

                if (_settings.RefreshSyncBeforeBuild)
                {
                    AppendLog("폴더 동기화 (/RefreshSync)...", LogKind.Info);
                    var syncCode = await AdvInstaller.RunAsync(_cliPath!, AdvInstaller.RefreshSyncArguments(file), OnLogLine, ct);
                    if (syncCode != 0)
                    {
                        AppendLog(syncCode == -1 ? "중지됨" : $"폴더 동기화 실패 (종료 코드 {syncCode})", LogKind.Error);
                        fail++;
                        continue;
                    }
                }

                var fileSw = Stopwatch.StartNew();
                var code = await AdvInstaller.RunAsync(_cliPath!, AdvInstaller.BuildArguments(file, rebuild, buildName), OnLogLine, ct);
                if (code == 0)
                {
                    ok++;
                    AppendLog($"✓ 성공 ({fileSw.Elapsed.TotalSeconds:N1}초)", LogKind.Success);
                }
                else
                {
                    fail++;
                    AppendLog(code == -1 ? "■ 중지됨" : $"✗ 빌드 실패 (종료 코드 {code})", LogKind.Error);
                }
                RefreshFolderItem(file);
            }
        }
        catch (Exception ex)
        {
            fail++;
            AppendLog(ex.Message, LogKind.Error);
        }
        finally
        {
            _building = false;
            _buildCts.Dispose();
            _buildCts = null;
            UpdateUiState();
        }

        var elapsed = sw.Elapsed.TotalSeconds < 60 ? $"{sw.Elapsed.TotalSeconds:N1}초" : $"{sw.Elapsed:m\\:ss}";
        var summary = files.Count == 1
            ? (fail == 0 ? $"✓ 빌드 성공 · {elapsed}" : $"✗ 빌드 실패 · {elapsed}")
            : $"완료 — 성공 {ok} · 실패 {fail} · {elapsed}";
        SetBuildState(summary, fail == 0 ? "SuccessBrush" : "DangerBrush");
        SetStatus(summary + (_lastOutputPath != null ? $"  →  {_lastOutputPath}" : ""));
        if (_current?.Kind == NavKind.Overview) RenderOverview();
        return (ok, fail);
    }

    /// <summary>동기화 폴더를 다시 읽어 프로젝트에 반영 (CLI가 파일을 직접 고친 뒤 다시 불러옴)</summary>
    private async Task RefreshSyncAsync()
    {
        if (_doc?.FilePath == null || _building || !EnsureCli()) return;
        if (!CommitPendingEdits()) return;
        if (_dirty)
        {
            if (!DarkDialog.Confirm(this, "동기화는 디스크의 파일을 직접 수정합니다.\n변경 사항을 먼저 저장할까요?", "저장 후 동기화"))
                return;
            if (!SaveDocument()) return;
        }

        Backup.Create(_doc.FilePath);
        ShowLog(clear: true);
        _building = true;
        UpdateUiState();
        _buildCts = new CancellationTokenSource();
        try
        {
            AppendLog($"━━ 폴더 동기화: {Path.GetFileName(_doc.FilePath)}", LogKind.Header);
            SetBuildState("폴더 동기화 중...", "AccentBrush");
            var code = await AdvInstaller.RunAsync(_cliPath!, AdvInstaller.RefreshSyncArguments(_doc.FilePath), OnLogLine, _buildCts.Token);
            SetBuildState(code == 0 ? "✓ 동기화 완료" : $"✗ 동기화 실패 (종료 코드 {code})", code == 0 ? "SuccessBrush" : "DangerBrush");
            AppendLog(code == 0 ? "✓ 동기화 완료 — 프로젝트를 다시 불러옵니다" : $"✗ 실패 (종료 코드 {code})", code == 0 ? LogKind.Success : LogKind.Error);
        }
        finally
        {
            _building = false;
            _buildCts.Dispose();
            _buildCts = null;
            UpdateUiState();
        }
        await ReloadIfChangedOnDiskAsync();
    }

    /// <summary>CLI 작업 후 파일이 바뀌었으면(동기화·버전 변환) 조용히 다시 불러오기</summary>
    private async Task ReloadIfChangedOnDiskAsync()
    {
        if (_doc?.FilePath == null || _dirty || !File.Exists(_doc.FilePath)) return;
        if (File.GetLastWriteTimeUtc(_doc.FilePath) == _diskWriteTimeUtc) return;
        await ReloadFromDiskAsync();
        SetStatus(TxtStatus.Text + "  ·  Advanced Installer가 프로젝트를 갱신해 다시 불러왔습니다.");
    }

    // ── 로그 ────────────────────────────────────────────────────────

    private void OnLogLine(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnLogLine(line));
            return;
        }
        if (string.IsNullOrWhiteSpace(line)) return;

        const string building = "Building package:";
        var idx = line.IndexOf(building, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            _lastOutputPath = line[(idx + building.Length)..].Trim();
            BtnOpenOutput.IsEnabled = true;
        }

        var kind = line.Contains("finished successfully", StringComparison.OrdinalIgnoreCase) ? LogKind.Success
            : line.TrimStart().StartsWith('[') && line.TrimEnd().EndsWith(']') ? LogKind.Header
            : ErrorRegex.IsMatch(line) ? LogKind.Error
            : WarnRegex.IsMatch(line) ? LogKind.Warn
            : LogKind.Normal;
        AppendLog(line, kind);
    }

    private void AppendLog(string text, LogKind kind)
    {
        var brush = kind switch
        {
            LogKind.Header => (Brush)FindResource("AccentBrush"),
            LogKind.Info => (Brush)FindResource("InfoBrush"),
            LogKind.Success => (Brush)FindResource("SuccessBrush"),
            LogKind.Warn => new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x6B)),
            LogKind.Error => (Brush)FindResource("DangerBrush"),
            _ => new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD0)),
        };
        var tb = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontWeight = kind == LogKind.Header ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(0, kind == LogKind.Header && LstLog.Items.Count > 0 ? 8 : 0, 0, 0),
        };
        LstLog.Items.Add(tb);
        while (LstLog.Items.Count > MaxLogLines) LstLog.Items.RemoveAt(0);
        LstLog.ScrollIntoView(tb);
    }

    private void SetBuildState(string text, string brushKey)
    {
        TxtBuildState.Text = text;
        TxtBuildState.Foreground = (Brush)FindResource(brushKey);
    }

    private void ShowLog(bool clear)
    {
        if (clear) LstLog.Items.Clear();
        RowLogSplitter.Height = new GridLength(5);
        RowLog.Height = new GridLength(Math.Clamp(_settings.LogPanelHeight, 120, 700));
        BtnShowLog.Visibility = Visibility.Collapsed;
    }

    private void BtnCloseLog_Click(object sender, RoutedEventArgs e)
    {
        if (RowLog.ActualHeight > 0) _settings.LogPanelHeight = RowLog.ActualHeight;
        RowLogSplitter.Height = new GridLength(0);
        RowLog.Height = new GridLength(0);
        BtnShowLog.Visibility = Visibility.Visible;
    }

    private void BtnShowLog_Click(object sender, RoutedEventArgs e) => ShowLog(clear: false);

    private void LogSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _settings.LogPanelHeight = RowLog.ActualHeight;
        _settings.Save();
    }

    private void BtnCancelBuild_Click(object sender, RoutedEventArgs e)
    {
        _buildCts?.Cancel();
        SetBuildState("중지하는 중...", "DangerBrush");
    }

    private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutputPath == null) return;
        if (File.Exists(_lastOutputPath)) OpenInExplorer(_lastOutputPath);
        else if (Path.GetDirectoryName(_lastOutputPath) is { } dir && System.IO.Directory.Exists(dir)) OpenInExplorer(dir);
    }

    private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var item in LstLog.Items)
            if (item is TextBlock tb) sb.AppendLine(tb.Text);
        if (sb.Length == 0) return;
        Clipboard.SetText(sb.ToString());
        SetStatus("빌드 로그 복사됨");
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => LstLog.Items.Clear();
}
