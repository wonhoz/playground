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
    private static readonly string[] Extensions = [".hwx", ".hwp", ".hwpx"];
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "Hwx.Pdf");

    private readonly ObservableCollection<ConvertItem> _items = [];
    private readonly BlockingCollection<ConvertItem> _queue = [];
    private volatile bool _cancelRequested;
    private int _pending;          // 대기 + 변환 중 개수 (작업 스레드와 공유)
    private string? _outputDir;    // null = 원본과 같은 폴더
    private bool _overwrite;
    private bool? _securityModule;

    public MainWindow(string[] args)
    {
        InitializeComponent();
        FileList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OverwriteCheck.Checked += (_, _) => _overwrite = true;
        OverwriteCheck.Unchecked += (_, _) => _overwrite = false;

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
            MessageBox.Show(this, "한컴오피스 한글이 설치되어 있지 않아 변환할 수 없어요.", "HWX → PDF",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var added = 0;
        foreach (var file in Expand(paths))
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
            SummaryText.Text = "변환할 .hwx · .hwp · .hwpx 파일을 찾지 못했어요.";
        RefreshSummary();
    }

    private static IEnumerable<string> Expand(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(IsTarget).Order().ToList(); }
                catch { continue; }
                foreach (var f in files) yield return f;
            }
            else if (File.Exists(path) && IsTarget(path))
                yield return Path.GetFullPath(path);
        }
    }

    private static bool IsTarget(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private void Enqueue(ConvertItem item)
    {
        item.State = ConvertState.Waiting;
        item.Message = "";
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
                    item.Message = _securityModule == true
                        ? "한글로 PDF 만드는 중…"
                        : "한글로 PDF 만드는 중… (한글 확인 창이 뜨면 '접근 허용' 을 눌러 주세요)";
                    SetMood(Mood.Busy);
                });
                try
                {
                    converter ??= new HwpPdfConverter();
                    var pdf = OutputPathFor(item.Source);
                    converter.Convert(item.Source, pdf, TempDir);
                    _securityModule = converter.SecurityModuleActive;
                    Ui(() => { item.OutputPath = pdf; item.State = ConvertState.Done; item.Message = _outputDir == null ? $"→ {Path.GetFileName(pdf)}" : $"→ {pdf}"; });
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

    private string OutputPathFor(string source)
    {
        var dir = _outputDir ?? Path.GetDirectoryName(source)!;
        var name = Path.GetFileNameWithoutExtension(source);
        var pdf = Path.Combine(dir, name + ".pdf");
        if (_overwrite) return pdf;
        for (var n = 2; File.Exists(pdf); n++)
            pdf = Path.Combine(dir, $"{name} ({n}).pdf");
        return pdf;
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
            SummaryText.Text = "결재 문서(hwx) · 한글(hwp · hwpx) 파일이나 폴더를 끌어다 놓으면 바로 PDF 로 바꿔 드려요.";
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
            text += "  —  모두 끝났어요! 줄을 두 번 누르면 PDF 가 열려요.";
        }
        if (waiting == 0 && _securityModule == false)
            text += "\n💡 한글 보안 모듈(FilePathCheckerModule*.dll)을 이 exe 옆에 두면 확인 창 없이 변환돼요.";
        SummaryText.Text = text;
        RetryButton.IsEnabled = failed + canceled > 0;
    }

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
            Filter = "결재 · 한글 문서 (*.hwx;*.hwp;*.hwpx)|*.hwx;*.hwp;*.hwpx|모든 파일 (*.*)|*.*",
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
        var dlg = new OpenFolderDialog { Title = "PDF 를 저장할 폴더" };
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
            MessageBox.Show(this, "아직 변환 중이에요. 그래도 닫을까요?", "HWX → PDF",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _cancelRequested = true;
        _queue.CompleteAdding();
    }
}
