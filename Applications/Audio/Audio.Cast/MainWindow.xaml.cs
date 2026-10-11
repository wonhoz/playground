using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AudioCast.Models;
using AudioCast.Services;

namespace AudioCast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    static readonly int[] ParallelChoices = [1, 2, 4, 8];

    readonly ObservableCollection<FileJob> _jobs = [];
    readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    AppSettings _settings = new();
    string? _ffmpegPath;
    bool _converting;
    CancellationTokenSource? _cts;
    string? _lastOutputDir;

    public MainWindow()
    {
        InitializeComponent();
        LbJobs.ItemsSource = _jobs;
        _progressTimer.Tick += (_, _) => UpdateOverallProgress();
    }

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));

        foreach (var f in AudioConverter.InputFilters)
            CbInput.Items.Add(new ComboBoxItem { Content = f.Label, Tag = f.Tag });
        foreach (var f in OutputPresets.All)
            CbOutput.Items.Add(f);
        foreach (var n in ParallelChoices)
            CbParallel.Items.Add(new ComboBoxItem { Content = $"{n}개", Tag = n });

        _settings = AppSettings.Load();
        ApplySettings();
        CheckFfmpeg();

        // exe에 파일·폴더를 끌어다 놓거나 '보내기'로 실행한 경우 바로 변환
        var startupPaths = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (startupPaths.Length > 0) _ = StartAsync(startupPaths);
    }

    void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();
        SaveSettings();
    }

    // ─── 설정 적용 / 저장 ─────────────────────────────────────────────────
    void ApplySettings()
    {
        CbInput.SelectedItem = CbInput.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag as string == _settings.InputFilter)
                               ?? CbInput.Items[0];

        CbOutput.SelectedItem = OutputPresets.Get(_settings.OutputFormat);
        FillQualities(_settings.QualityKey);

        ChkSmart.IsChecked     = _settings.SmartQuality;
        ChkTags.IsChecked      = _settings.KeepTags;
        ChkOverwrite.IsChecked = _settings.Overwrite;
        ChkStructure.IsChecked = _settings.KeepStructure;
        TbOutputDir.Text       = string.IsNullOrWhiteSpace(_settings.OutputDir) ? AppSettings.DefaultOutputDir : _settings.OutputDir;
        (_settings.UseSourceDir ? RbSourceDir : RbCustomDir).IsChecked = true;
        UpdateOutputModeUi();

        var parallel = ParallelChoices.Contains(_settings.Parallelism) ? _settings.Parallelism : 2;
        CbParallel.SelectedItem = CbParallel.Items.Cast<ComboBoxItem>().First(i => (int)i.Tag == parallel);

        if (_settings.LastResultText is not null)
        {
            SetStatus($"이전: {_settings.LastResultText}");
            _lastOutputDir = _settings.LastOutputDir;
            if (_lastOutputDir is not null && Directory.Exists(_lastOutputDir))
                BtnOpenFolder.Visibility = Visibility.Visible;
        }
    }

    void SaveSettings()
    {
        _settings.InputFilter   = GetInputFilterTag();
        _settings.OutputFormat  = SelectedFormat.Tag;
        _settings.QualityKey    = SelectedQuality.Key;
        _settings.SmartQuality  = ChkSmart.IsChecked == true;
        _settings.KeepTags      = ChkTags.IsChecked == true;
        _settings.Overwrite     = ChkOverwrite.IsChecked == true;
        _settings.KeepStructure = ChkStructure.IsChecked == true;
        _settings.UseSourceDir  = RbSourceDir.IsChecked == true;
        _settings.OutputDir     = TbOutputDir.Text.Trim();
        _settings.Parallelism   = GetParallelism();
        _settings.LastOutputDir = _lastOutputDir;
        _settings.Save();
    }

    // ─── 포맷 / 품질 ─────────────────────────────────────────────────────────
    FormatSpec SelectedFormat => CbOutput.SelectedItem as FormatSpec ?? OutputPresets.All[0];

    QualityOption SelectedQuality => CbQuality.SelectedItem as QualityOption ?? SelectedFormat.Qualities[0];

    void FillQualities(string? preferredKey)
    {
        var fmt = SelectedFormat;
        CbQuality.Items.Clear();
        foreach (var q in fmt.Qualities) CbQuality.Items.Add(q);

        // 같은 키 → 가장 가까운 비트레이트 → 첫 항목 순으로 선택 유지
        var target = fmt.Qualities.FirstOrDefault(q => q.Key == preferredKey)
                     ?? (int.TryParse(preferredKey, out var kbps) && !fmt.Lossless
                         ? fmt.Qualities.OrderBy(q => Math.Abs(q.Kbps - kbps)).First()
                         : fmt.Qualities[0]);
        CbQuality.SelectedItem = target;
        UpdateFormatHint();
    }

    void CbOutput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var prevKey = e.RemovedItems.Count > 0 && CbQuality.SelectedItem is QualityOption q ? q.Key : null;
        FillQualities(prevKey);
    }

    void CbQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateFormatHint();
    }

    void UpdateFormatHint()
    {
        var fmt = SelectedFormat;
        var q   = SelectedQuality;

        string hint = fmt.Format switch
        {
            OutputFormat.Flac    => "무손실 압축 · 원본 음질 100% 보존 · WAV 대비 약 50~60% 용량",
            OutputFormat.Wav     => "무압축 PCM · 원본 음질 100% 보존 · 용량 큼 (CD 품질 기준 약 10 MB/분)",
            OutputFormat.M4aAlac => "무손실 압축 · 원본 음질 100% 보존 · iPhone·iTunes 등 Apple 기기 친화",
            _ => $"손실 압축 · {(q.Vbr ? "평균 약 " : "")}{q.Kbps} kbps · 약 {q.Kbps * 60 / 8 / 1000.0:0.0} MB/분 · " + fmt.Format switch
            {
                OutputFormat.Mp3    => q.Vbr ? "가변 비트레이트, 320 kbps와 체감 차이 거의 없음" : "거의 모든 기기·차량 오디오 호환",
                OutputFormat.M4aAac => "같은 비트레이트에서 MP3보다 고음질",
                OutputFormat.Ogg    => "오픈 포맷 · 게임·안드로이드에서 많이 사용",
                OutputFormat.Opus   => "낮은 비트레이트에서 최고 효율 · 일부 구형 기기 미지원",
                _ => ""
            }
        };
        if (q.CdQuality)
            hint += " · 고해상도 원본은 16bit/44.1kHz로 다운샘플";
        else if (fmt.Lossless)
            hint += " · 원본 비트 심도(24bit)·샘플레이트 그대로";

        TbFormatHint.Text       = hint;
        TbLossyWarn.Visibility  = fmt.Lossless ? Visibility.Collapsed : Visibility.Visible;
    }

    // ─── 저장 위치 ───────────────────────────────────────────────────────────
    void OutputMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateOutputModeUi();
    }

    void UpdateOutputModeUi()
    {
        bool custom = RbCustomDir.IsChecked == true;
        TbOutputDir.IsEnabled  = custom;
        BtnBrowse.IsEnabled    = custom;
        ChkStructure.IsEnabled = custom;
    }

    void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "변환 파일을 저장할 폴더 선택" };
        var current = TbOutputDir.Text.Trim();
        if (Directory.Exists(current)) dlg.InitialDirectory = current;
        if (dlg.ShowDialog(this) == true)
            TbOutputDir.Text = dlg.FolderName;
    }

    // ─── FFmpeg ──────────────────────────────────────────────────────────────
    void CheckFfmpeg()
    {
        _ffmpegPath = FfmpegLocator.Find(_settings.FfmpegPath);
        FfmpegPanel.Visibility = _ffmpegPath is null ? Visibility.Visible : Visibility.Collapsed;
        if (_ffmpegPath is not null && FfmpegLocator.FindProbe(_ffmpegPath) is null)
            SetStatus("ffprobe.exe가 없어 음질 보호·진행률 표시가 제한됩니다");
    }

    void BtnRecheckFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        CheckFfmpeg();
        if (_ffmpegPath is null) TbFfmpegLog.Text = "여전히 찾을 수 없습니다.";
        else SetStatus($"FFmpeg 확인: {_ffmpegPath}");
    }

    void BtnPickFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "ffmpeg.exe 선택",
            Filter = "ffmpeg.exe|ffmpeg.exe|실행 파일 (*.exe)|*.exe",
        };
        if (dlg.ShowDialog(this) != true) return;
        _settings.FfmpegPath = dlg.FileName;
        CheckFfmpeg();
        if (_ffmpegPath is not null) SetStatus($"FFmpeg 지정: {_ffmpegPath}");
    }

    async void BtnInstallFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        BtnInstallFfmpeg.IsEnabled = false;
        BtnInstallFfmpeg.Content   = "설치 중…";
        TbFfmpegStatus.Text        = "winget으로 FFmpeg 설치 중… (1~3분 소요)";
        try
        {
            var psi = new ProcessStartInfo("winget", "install --id Gyan.FFmpeg -e --accept-source-agreements --accept-package-agreements")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("winget을 실행할 수 없습니다.");
            DataReceivedEventHandler log = (_, a) =>
            {
                var text = a.Data?.Trim();
                // winget 진행 스피너(- \ | /)·진행 막대 줄은 생략
                if (string.IsNullOrEmpty(text) || text.Length <= 2) return;
                Dispatcher.BeginInvoke(() => TbFfmpegLog.Text = text);
            };
            proc.OutputDataReceived += log;
            proc.ErrorDataReceived  += log;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            TbFfmpegLog.Text = $"설치 실패: {ex.Message}";
        }

        CheckFfmpeg();
        if (_ffmpegPath is not null)
        {
            SetStatus("FFmpeg 설치 완료 — 이제 파일을 드롭하세요");
        }
        else
        {
            TbFfmpegStatus.Text      = "설치 후에도 ffmpeg.exe를 찾지 못했습니다";
            TbFfmpegLog.Text         = "터미널에서 winget install Gyan.FFmpeg 실행 후 '다시 찾기'를 누르세요.";
            BtnInstallFfmpeg.Content = "다시 설치";
            BtnInstallFfmpeg.IsEnabled = true;
        }
    }

    // ─── 드래그 & 드롭 ──────────────────────────────────────────────────────
    void Window_DragEnter(object sender, DragEventArgs e)
    {
        bool ok = !_converting && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } dropped &&
                  AudioConverter.HasSupportedFiles(dropped, GetInputFilterTag());
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        if (ok) AnimateDropZone(true);
    }

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_converting && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void Window_DragLeave(object sender, DragEventArgs e) => AnimateDropZone(false);

    async void Window_Drop(object sender, DragEventArgs e)
    {
        AnimateDropZone(false);
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } dropped) return;
        await StartAsync(dropped);
    }

    void AnimateDropZone(bool hover)
    {
        var target = hover ? Color.FromRgb(0xFF, 0x9F, 0x43) : Color.FromRgb(0x3A, 0x3A, 0x48);
        DropBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, TimeSpan.FromMilliseconds(150)));
    }

    void BtnPickFiles_Click(object sender, RoutedEventArgs e) => _ = PickFilesAsync();

    async Task PickFilesAsync()
    {
        if (_converting) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title       = "변환할 오디오 파일 선택",
            Multiselect = true,
            Filter      = "오디오 파일|*.mp3;*.flac;*.wav;*.m4a;*.aac;*.ogg;*.oga;*.opus;*.wma;*.aiff;*.aif;*.ape;*.wv;*.tta;*.mka|" +
                          "영상 파일 (오디오 추출)|*.mp4;*.mkv;*.webm;*.mov;*.avi;*.m4v|모든 파일|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            await StartAsync(dlg.FileNames);
    }

    // ─── 변환 실행 ───────────────────────────────────────────────────────────
    async Task StartAsync(string[] paths)
    {
        try
        {
            if (_converting)
            {
                SetStatus("변환 중입니다. 완료 후 다시 드롭하세요.");
                return;
            }
            if (_ffmpegPath is null)
            {
                CheckFfmpeg();
                if (_ffmpegPath is null)
                {
                    SetStatus("FFmpeg가 필요합니다 — 위 안내에서 설치하거나 경로를 지정하세요");
                    return;
                }
            }

            string? outputDir = null;
            if (RbCustomDir.IsChecked == true)
            {
                outputDir = TbOutputDir.Text.Trim();
                if (outputDir.Length == 0 || !Path.IsPathFullyQualified(outputDir))
                {
                    DarkMessageBox.Show("저장 폴더 경로가 올바르지 않습니다.\n'찾아보기'로 폴더를 지정해 주세요.", "저장 위치", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Directory.CreateDirectory(outputDir);
            }

            var jobs = AudioConverter.CollectFiles(paths, GetInputFilterTag());
            if (jobs.Count == 0)
            {
                var filter = (CbInput.SelectedItem as ComboBoxItem)?.Content;
                SetStatus($"변환할 파일이 없습니다 (입력 파일: {filter})");
                return;
            }

            _jobs.Clear();
            foreach (var j in jobs) _jobs.Add(j);
            DropHint.Visibility = Visibility.Collapsed;
            JobPanel.Visibility = Visibility.Visible;

            _lastOutputDir = outputDir ?? Path.GetDirectoryName(jobs[0].SourcePath);
            BtnOpenFolder.Visibility = Visibility.Collapsed;

            var opt = new ConvertOptions(
                SelectedFormat,
                SelectedQuality,
                ChkSmart.IsChecked == true,
                ChkTags.IsChecked == true,
                ChkOverwrite.IsChecked == true,
                outputDir,
                ChkStructure.IsChecked == true,
                GetParallelism());

            await RunConversionAsync(jobs, opt);
        }
        catch (Exception ex)
        {
            SetStatus($"오류: {ex.Message}");
        }
    }

    async Task RunConversionAsync(List<FileJob> jobs, ConvertOptions opt)
    {
        _converting = true;
        _cts = new CancellationTokenSource();
        SetBusyUi(true);
        _progressTimer.Start();

        ConversionResult result;
        try
        {
            result = await Task.Run(() => AudioConverter.ConvertAsync(jobs, opt, _ffmpegPath!, _cts.Token));
        }
        finally
        {
            _progressTimer.Stop();
            _converting = false;
            _cts.Dispose();
            _cts = null;
            SetBusyUi(false);
        }

        UpdateOverallProgress();
        ShowResult(result, jobs);
    }

    void SetBusyUi(bool busy)
    {
        PBar.Visibility         = busy ? Visibility.Visible : Visibility.Collapsed;
        PBar.Value              = 0;
        BtnCancel.Visibility    = busy ? Visibility.Visible : Visibility.Collapsed;
        BtnCancel.IsEnabled     = true;
        BtnClearList.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
    }

    void UpdateOverallProgress()
    {
        if (_jobs.Count == 0) return;
        int finished = _jobs.Count(j => j.Status is not (JobStatus.Pending or JobStatus.Running));
        double sum   = _jobs.Sum(j => j.Status == JobStatus.Running ? j.Progress : j.Status == JobStatus.Pending ? 0 : 1);
        PBar.Value   = sum / _jobs.Count * 100;

        int running = _jobs.Count(j => j.Status == JobStatus.Running);
        TbJobSummary.Text = _converting
            ? $"{_jobs.Count}개 중 {finished}개 처리 · 변환 중 {running}개"
            : $"{_jobs.Count}개 파일";

        if (_converting)
        {
            var current = _jobs.FirstOrDefault(j => j.Status == JobStatus.Running);
            if (current is not null) SetStatus($"변환 중: {current.FileName}  ({finished} / {_jobs.Count})");
        }
    }

    void ShowResult(ConversionResult r, List<FileJob> jobs)
    {
        var parts = new List<string> { $"변환 {r.Success}개" };
        if (r.Copied  > 0) parts.Add($"재인코딩 없이 복사 {r.Copied}개");
        if (r.Skipped > 0) parts.Add($"건너뜀 {r.Skipped}개");
        if (r.Failed  > 0) parts.Add($"실패 {r.Failed}개");
        var msg = (r.Cancelled ? "취소됨 — " : "완료 — ") + string.Join(" / ", parts);

        SetStatus(msg);
        TbJobSummary.Text = $"{jobs.Count}개 파일 · {msg}";
        _settings.LastResultText = msg;

        if (r.Success + r.Copied > 0 && _lastOutputDir is not null && Directory.Exists(_lastOutputDir))
            BtnOpenFolder.Visibility = Visibility.Visible;

        if (r.Failed > 0 && !r.Cancelled)
        {
            var failed = jobs.Where(j => j.Status == JobStatus.Failed).ToList();
            var sb = new StringBuilder($"✅ 성공: {r.Success + r.Copied}개\n❌ 실패: {r.Failed}개\n\n");
            foreach (var j in failed.Take(8))
                sb.Append($"• {j.FileName}\n  {j.Detail}\n");
            if (failed.Count > 8) sb.Append($"…외 {failed.Count - 8}개");
            DarkMessageBox.Show(sb.ToString().TrimEnd(), "변환 완료 (일부 실패)", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ─── 이벤트 핸들러 ───────────────────────────────────────────────────────
    void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.IsEnabled = false;
        SetStatus("취소 중…");
    }

    void BtnClearList_Click(object sender, RoutedEventArgs e)
    {
        if (_converting) return;
        _jobs.Clear();
        JobPanel.Visibility = Visibility.Collapsed;
        DropHint.Visibility = Visibility.Visible;
        SetStatus("파일을 드롭하면 바로 변환을 시작합니다");
    }

    void BtnHelp_Click(object sender, RoutedEventArgs e) => HelpPopup.IsOpen = !HelpPopup.IsOpen;

    void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutputDir is not null && Directory.Exists(_lastOutputDir))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_lastOutputDir}\"") { UseShellExecute = true });
    }

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _converting)
        {
            BtnCancel_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            HelpPopup.IsOpen = !HelpPopup.IsOpen;
            e.Handled = true;
        }
        else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = PickFilesAsync();
            e.Handled = true;
        }
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────
    void SetStatus(string text) => TbStatus.Text = text;

    string GetInputFilterTag() => (CbInput.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";

    int GetParallelism() => (CbParallel.SelectedItem as ComboBoxItem)?.Tag as int? ?? 2;
}
