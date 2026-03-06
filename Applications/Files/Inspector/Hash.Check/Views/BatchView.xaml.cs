using HashCheck.Services;
using Microsoft.Win32;

namespace HashCheck.Views;

public class ChecksumEntryVm : ChecksumEntry
{
    public string AlgoText => Algorithm switch
    {
        HashAlgorithmKind.MD5    => "MD5",
        HashAlgorithmKind.SHA1   => "SHA-1",
        HashAlgorithmKind.SHA256 => "SHA-256",
        HashAlgorithmKind.SHA512 => "SHA-512",
        _                        => "?"
    };
}

public partial class BatchView : UserControl
{
    private readonly HashService _svc = new();
    private CancellationTokenSource? _cts;
    private string? _checksumDir;
    private List<ChecksumEntryVm> _entries = [];

    public BatchView()
    {
        InitializeComponent();
    }

    // ── 파일 처리 ─────────────────────────────────────────────────
    private void LoadChecksumFile(string path)
    {
        try
        {
            var (entries, _) = ChecksumParser.Parse(path);
            _entries = entries.Select(e => new ChecksumEntryVm
            {
                ExpectedHash = e.ExpectedHash,
                FilePath     = e.FilePath,
                Algorithm    = e.Algorithm,
                ActualHash   = e.ActualHash,
                IsMatch      = e.IsMatch
            }).ToList();

            _checksumDir = Path.GetDirectoryName(path) ?? "";
            TxtChecksumFile.Text = Path.GetFileName(path);
            TxtEntryCount.Text   = $"항목 {_entries.Count}개";
            DropHint.Visibility    = Visibility.Collapsed;
            FileInfoBar.Visibility = Visibility.Visible;
            LvResults.ItemsSource  = _entries;
            TxtSummary.Text        = $"{_entries.Count}개 항목 로드됨. '검증 시작'을 클릭하세요.";
            BtnExport.IsEnabled    = false;

            (Window.GetWindow(this) as MainWindow)?.SetStatus($"로드: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"체크섬 파일 파싱 오류:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnVerify_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.Count == 0 || string.IsNullOrEmpty(_checksumDir)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        BtnVerify.IsEnabled = false;

        var mw = Window.GetWindow(this) as MainWindow;
        mw?.ShowProgress(true, false);

        int done = 0, match = 0, mismatch = 0, missing = 0;

        foreach (var entry in _entries)
        {
            if (_cts.Token.IsCancellationRequested) break;

            var fullPath = Path.Combine(_checksumDir, entry.FilePath);
            try
            {
                entry.ActualHash = await _svc.ComputeAsync(fullPath, entry.Algorithm, ct: _cts.Token);
                entry.IsMatch    = HashService.HashEquals(entry.ExpectedHash, entry.ActualHash);
                if (entry.IsMatch == true) match++; else mismatch++;
            }
            catch (FileNotFoundException)
            {
                entry.ActualHash = "(파일 없음)";
                entry.IsMatch    = false;
                missing++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                entry.ActualHash = $"(오류: {ex.Message})";
                entry.IsMatch    = false;
                mismatch++;
            }

            done++;
            mw?.ShowProgress(true, false, done, _entries.Count);
        }

        // ListView 갱신 (INotifyPropertyChanged 없으므로 리바인딩)
        LvResults.ItemsSource = null;
        LvResults.ItemsSource = _entries;

        TxtSummary.Text     = $"완료: 일치 {match}개 / 불일치 {mismatch}개 / 누락 {missing}개";
        BtnExport.IsEnabled = _entries.Count > 0;
        BtnVerify.IsEnabled = true;
        mw?.ShowProgress(false);
        mw?.SetStatus($"검증 완료 — 일치 {match} / 불일치 {mismatch} / 누락 {missing}");
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _entries.Clear();
        _checksumDir = null;
        LvResults.ItemsSource  = null;
        DropHint.Visibility    = Visibility.Visible;
        FileInfoBar.Visibility = Visibility.Collapsed;
        TxtSummary.Text        = "파일을 불러오면 검증 결과가 표시됩니다.";
        BtnExport.IsEnabled    = false;
        (Window.GetWindow(this) as MainWindow)?.SetStatus("준비");
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "CSV 내보내기",
            Filter     = "CSV 파일|*.csv",
            DefaultExt = "csv",
            FileName   = $"batch_verify_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = new List<string> { "상태,파일명,알고리즘,예상 해시,실제 해시" };
            foreach (var e2 in _entries)
            {
                var status = e2.IsMatch switch { true => "일치", false => "불일치", _ => "미검증" };
                lines.Add($"{status},{CsvEscape(e2.FilePath)},{e2.AlgoText},{e2.ExpectedHash},{e2.ActualHash ?? ""}");
            }
            File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
            (Window.GetWindow(this) as MainWindow)?.SetStatus($"CSV 저장: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CSV 저장 오류:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string CsvEscape(string s)
        => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    // ── 드래그&드롭 / 파일 열기 ──────────────────────────────────
    private void View_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void View_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0
            && File.Exists(files[0]))
            LoadChecksumFile(files[0]);
    }

    private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "체크섬 파일 선택",
            Filter = "체크섬 파일|*.md5;*.sha1;*.sha256;*.sha512;*.checksum;*.sfv|모든 파일|*.*"
        };
        if (dlg.ShowDialog() == true) LoadChecksumFile(dlg.FileName);
    }
}
