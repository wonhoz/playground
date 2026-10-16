using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HwxPdf.Models;
using HwxPdf.Services;
using Microsoft.Win32;

namespace HwxPdf;

public partial class MainWindow : Window
{
    private static readonly string[] HwpExtensions = [".hwx", ".hwp", ".hwpx"];
    private static readonly string[] PdfExtensions = [".pdf"];
    private const string AppTitle = "한글 ⇄ PDF";
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "Hwx.Pdf");

    private readonly ObservableCollection<ConvertItem> _items = [];
    private readonly BlockingCollection<ConvertItem> _queue = [];
    private volatile bool _cancelRequested;
    private int _pending;          // 대기 + 변환 중 개수 (작업 스레드와 공유)
    private string? _outputDir;    // null = 원본과 같은 폴더
    private bool _overwrite;
    private bool? _securityModule;
    private ConvertDirection _mode = ConvertDirection.ToPdf;   // 지금 탭 — 폴더 검색 · 파일 추가 필터 · 안내 문구
    private bool _saveHwpx;

    public MainWindow(string[] args)
    {
        InitializeComponent();
        FileList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OverwriteCheck.Checked += (_, _) => _overwrite = true;
        OverwriteCheck.Unchecked += (_, _) => _overwrite = false;
        HwpxCheck.Checked += (_, _) => _saveHwpx = true;
        HwpxCheck.Unchecked += (_, _) => _saveHwpx = false;
        ApplyMode();

        var worker = new Thread(WorkerLoop) { IsBackground = true, Name = "HwpWorker" };
        worker.SetApartmentState(ApartmentState.STA);   // 한글 COM 은 STA 에서
        worker.Start();

        Loaded += (_, _) =>
        {
            if (!HwpPdfConverter.IsHancomInstalled)
            {
                SetMood(Mood.Sad);
                SummaryText.Text = "한컴오피스 한글이 설치되어 있지 않아요. 한글을 설치한 PC 에서 실행해 주세요.";
                return;
            }
            if (args.Length > 0) AddPaths(args);
        };
    }

    // ── 목록에 추가 → 바로 변환 ─────────────────────────────

    private void AddPaths(IEnumerable<string> paths)
    {
        if (!HwpPdfConverter.IsHancomInstalled)
        {
            MessageBox.Show(this, "한컴오피스 한글이 설치되어 있지 않아 변환할 수 없어요.", AppTitle,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var added = 0;
        foreach (var file in Expand(paths, _mode))
        {
            // 이미 대기 · 변환 중인 같은 파일은 다시 넣지 않는다
            if (_items.Any(i => i.State is ConvertState.Waiting or ConvertState.Running
                                && string.Equals(i.Source, file, StringComparison.OrdinalIgnoreCase)))
                continue;
            var item = new ConvertItem(file);
            _items.Add(item);
            Enqueue(item);
            added++;
        }
        if (added == 0)
            SummaryText.Text = _mode == ConvertDirection.ToPdf
                ? "변환할 .hwx · .hwp · .hwpx · .pdf 파일을 찾지 못했어요."
                : "변환할 .pdf 파일을 찾지 못했어요. (한글 문서 폴더는 '한글 → PDF' 탭에서 놓아 주세요)";
        RefreshSummary();
    }

    /// <summary>
    /// 폴더는 지금 탭의 종류만 찾고(한글 탭: hwx · hwp · hwpx, PDF 탭: pdf),
    /// 파일은 탭과 상관없이 네 종류 모두 받는다 — 방향은 확장자로 정해진다.
    /// </summary>
    private static IEnumerable<string> Expand(IEnumerable<string> paths, ConvertDirection mode)
    {
        var folderExts = mode == ConvertDirection.ToPdf ? HwpExtensions : PdfExtensions;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(f => HasExt(f, folderExts)).Order().ToList(); }
                catch { continue; }
                foreach (var f in files) yield return f;
            }
            else if (File.Exists(path) && (HasExt(path, HwpExtensions) || HasExt(path, PdfExtensions)))
                yield return Path.GetFullPath(path);
        }
    }

    private static bool HasExt(string path, string[] exts) =>
        exts.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private void Enqueue(ConvertItem item)
    {
        item.State = ConvertState.Waiting;
        item.Message = "";
        item.TargetExtension = item.Direction == ConvertDirection.ToPdf ? ".pdf" : _saveHwpx ? ".hwpx" : ".hwp";
        Interlocked.Increment(ref _pending);
        _cancelRequested = false;
        CancelButton.IsEnabled = true;
        _queue.Add(item);
    }

    // ── 작업 스레드 (STA) ─────────────────────────────────

    private void WorkerLoop()
    {
        HwpPdfConverter? converter = null;
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            if (_cancelRequested)
            {
                Ui(() => { item.State = ConvertState.Canceled; item.Message = "멈춤"; });
            }
            else
            {
                Ui(() =>
                {
                    item.State = ConvertState.Running;
                    item.Message = (item.Direction == ConvertDirection.ToPdf
                                       ? "한글로 PDF 만드는 중…"
                                       : "PDF 를 한글 문서로 바꾸는 중… (쪽이 많으면 시간이 걸려요)")
                                   + (_securityModule == true ? "" : " (한글 확인 창이 뜨면 '접근 허용' 을 눌러 주세요)");
                    SetMood(Mood.Busy);
                });
                try
                {
                    converter ??= new HwpPdfConverter();
                    var output = OutputPathFor(item);
                    if (item.Direction == ConvertDirection.ToPdf)
                        converter.ToPdf(item.Source, output, TempDir);
                    else
                        converter.PdfToHwp(item.Source, output);
                    _securityModule = converter.SecurityModuleActive;
                    Ui(() => { item.OutputPath = output; item.State = ConvertState.Done; item.Message = _outputDir == null ? $"→ {Path.GetFileName(output)}" : $"→ {output}"; });
                }
                catch (Exception ex)
                {
                    var msg = ex is System.Runtime.InteropServices.COMException
                        ? "한글과의 연결이 끊겼어요. '실패 다시' 로 다시 시도해 보세요."
                        : ex.Message;
                    Ui(() => { item.State = ConvertState.Failed; item.Message = msg; });
                }
            }

            if (Interlocked.Decrement(ref _pending) == 0)
            {
                // 모두 끝나면 한글을 닫는다 — 다음 드롭 때 다시 띄운다
                converter?.Dispose();
                converter = null;
                Ui(() => { CancelButton.IsEnabled = false; RefreshSummary(); });
            }
            else
                Ui(RefreshSummary);
        }
    }

    private string OutputPathFor(ConvertItem item)
    {
        var dir = _outputDir ?? Path.GetDirectoryName(item.Source)!;
        var name = Path.GetFileNameWithoutExtension(item.Source);
        var ext = item.TargetExtension;
        var output = Path.Combine(dir, name + ext);
        if (_overwrite) return output;
        for (var n = 2; File.Exists(output); n++)
            output = Path.Combine(dir, $"{name} ({n}){ext}");
        return output;
    }

    private void Ui(Action action)
    {
        try { Dispatcher.Invoke(action); }
        catch (TaskCanceledException) { }   // 창을 닫는 중
    }

    // ── 요약 · 마스코트 ──────────────────────────────────

    private enum Mood { Idle, Busy, Happy, Sad }

    private void SetMood(Mood mood) => Mouth.Data = Geometry.Parse(mood switch
    {
        Mood.Busy => "M29.5,37.5 Q32,34.5 34.5,37.5 Q32,40.5 29.5,37.5 Z",
        Mood.Happy => "M26.5,35 Q32,43 37.5,35",
        Mood.Sad => "M28,39.5 Q32,35.5 36,39.5",
        _ => "M28,36 Q32,40 36,36",
    });

    private void RefreshSummary()
    {
        if (_items.Count == 0)
        {
            SetMood(Mood.Idle);
            SummaryText.Text = IdleText();
            return;
        }

        int done = 0, failed = 0, waiting = 0, canceled = 0;
        foreach (var i in _items)
        {
            switch (i.State)
            {
                case ConvertState.Done: done++; break;
                case ConvertState.Failed: failed++; break;
                case ConvertState.Canceled: canceled++; break;
                default: waiting++; break;
            }
        }

        var text = $"완료 {done} · 실패 {failed} · 남음 {waiting}" + (canceled > 0 ? $" · 멈춤 {canceled}" : "");
        if (waiting > 0)
            SetMood(Mood.Busy);
        else if (failed > 0)
        {
            SetMood(Mood.Sad);
            text += "  —  실패한 줄의 사유를 확인해 주세요.";
        }
        else
        {
            SetMood(Mood.Happy);
            text += "  —  모두 끝났어요! 줄을 두 번 누르면 만든 파일이 열려요.";
        }
        if (waiting == 0 && _securityModule == false)
            text += "\n💡 한글 보안 모듈(FilePathCheckerModule*.dll)을 이 exe 옆에 두면 확인 창 없이 변환돼요.";
        SummaryText.Text = text;
        RetryButton.IsEnabled = failed + canceled > 0;
    }

    // ── 탭 ──────────────────────────────────────────────

    private void ModeTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _mode = PdfTab.IsChecked == true ? ConvertDirection.ToHwp : ConvertDirection.ToPdf;
        ApplyMode();
        RefreshSummary();
    }

    private void ApplyMode()
    {
        var toPdf = _mode == ConvertDirection.ToPdf;
        DropTitle.Text = toPdf
            ? "여기에 결재 · 한글 문서나 폴더를 끌어다 놓으세요"
            : "여기에 PDF 파일이나 폴더를 끌어다 놓으세요";
        DropSub.Text = toPdf
            ? "폴더는 안쪽의 .hwx · .hwp · .hwpx 를 모두 찾아 PDF 로 바꿔요. 한컴오피스 한글이 설치돼 있어야 해요."
            : "폴더는 안쪽의 .pdf 를 모두 찾아 한글 문서로 바꿔요. 글자는 고칠 수 있게, 그림은 그림으로 들어가요.";
        HwpxCheck.Visibility = toPdf ? Visibility.Collapsed : Visibility.Visible;
    }

    private string IdleText() => _mode == ConvertDirection.ToPdf
        ? "결재 문서(hwx) · 한글(hwp · hwpx) 파일이나 폴더를 끌어다 놓으면 바로 PDF 로 바꿔 드려요."
        : "PDF 파일이나 폴더를 끌어다 놓으면 바로 한글 문서(hwp)로 바꿔 드려요. 스캔한 PDF 는 그림으로만 들어가요.";

    // ── 드래그 앤 드롭 ────────────────────────────────────

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        DropDash.Opacity = 1;
        DropZone.Background = new SolidColorBrush(Color.FromRgb(0xC9, 0xEA, 0xE6));
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) => ResetDropZone();

    private void Window_Drop(object sender, DragEventArgs e)
    {
        ResetDropZone();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            AddPaths(paths);
    }

    private void ResetDropZone()
    {
        DropDash.Opacity = 0.55;
        DropZone.Background = (Brush)FindResource("TealSoft");
    }

    // ── 버튼 ─────────────────────────────────────────────

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = _mode == ConvertDirection.ToPdf
                ? "결재 · 한글 문서 (*.hwx;*.hwp;*.hwpx)|*.hwx;*.hwp;*.hwpx|모든 파일 (*.*)|*.*"
                : "PDF 문서 (*.pdf)|*.pdf|모든 파일 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true) AddPaths(dlg.FileNames);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        // 변환 중 · 대기 중인 줄은 남긴다
        foreach (var i in _items.Where(i => i.State is not (ConvertState.Waiting or ConvertState.Running)).ToList())
            _items.Remove(i);
        RefreshSummary();
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in _items.Where(i => i.State is ConvertState.Failed or ConvertState.Canceled).ToList())
            Enqueue(i);
        RefreshSummary();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // 지금 변환 중인 파일은 끝까지 하고, 남은 대기 파일은 건너뛴다
        _cancelRequested = true;
        CancelButton.IsEnabled = false;
    }

    private void SameFolderCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (SameFolderCheck.IsChecked == true)
        {
            _outputDir = null;
            OutputDirText.Text = "";
            return;
        }
        var dlg = new OpenFolderDialog { Title = "만든 파일을 저장할 폴더" };
        if (dlg.ShowDialog(this) == true)
        {
            _outputDir = dlg.FolderName;
            OutputDirText.Text = $"→ {_outputDir}";
        }
        else
            SameFolderCheck.IsChecked = true;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var target = (FileList.SelectedItem as ConvertItem)?.OutputPath
                     ?? _items.LastOrDefault(i => i.OutputPath != null)?.OutputPath;
        if (target != null && File.Exists(target))
            Process.Start("explorer.exe", $"/select,\"{target}\"");
        else if (_outputDir != null)
            Process.Start("explorer.exe", $"\"{_outputDir}\"");
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is not ConvertItem item) return;
        var target = item.State == ConvertState.Done && File.Exists(item.OutputPath) ? item.OutputPath! : item.Source;
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Volatile.Read(ref _pending) > 0 &&
            MessageBox.Show(this, "아직 변환 중이에요. 그래도 닫을까요?", AppTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _cancelRequested = true;
        _queue.CompleteAdding();
    }
}
