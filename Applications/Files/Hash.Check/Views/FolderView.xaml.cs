using HashCheck.Services;
using Microsoft.Win32;

namespace HashCheck.Views;

public class FolderHashItem
{
    public string FilePath     { get; set; } = "";
    public string FileName     => Path.GetFileName(FilePath);
    public string RelativePath { get; set; } = "";
    public long   FileSize     { get; set; }
    public string Hash         { get; set; } = "—";
    public string HashColor    { get; set; } = "#888899";
    public string SizeText => FileSize switch
    {
        >= 1_073_741_824 => $"{FileSize / 1_073_741_824.0:F2} GB",
        >= 1_048_576     => $"{FileSize / 1_048_576.0:F2} MB",
        >= 1_024         => $"{FileSize / 1024.0:F1} KB",
        _                => $"{FileSize} B"
    };
}

public partial class FolderView : UserControl
{
    private readonly HashService _svc = new();
    private CancellationTokenSource? _cts;
    private string? _folderPath;
    private List<FolderHashItem> _items = [];
    private HashAlgorithmKind _algo = HashAlgorithmKind.SHA256;

    public FolderView()
    {
        InitializeComponent();
    }

    // ── 알고리즘 변경 ────────────────────────────────────────────
    private void CbAlgo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _algo = CbAlgo.SelectedIndex switch
        {
            0 => HashAlgorithmKind.MD5,
            1 => HashAlgorithmKind.SHA1,
            2 => HashAlgorithmKind.SHA256,
            3 => HashAlgorithmKind.SHA512,
            _ => HashAlgorithmKind.SHA256
        };
    }

    // ── 폴더 선택 ────────────────────────────────────────────────
    private void BtnPickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "폴더 선택" };
        if (dlg.ShowDialog() != true) return;

        _folderPath = dlg.FolderName;
        TxtFolder.Text = _folderPath;
        TxtFolder.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        BtnCompute.IsEnabled = true;

        // 파일 목록 미리 로드
        LoadFileList();
    }

    private void LoadFileList()
    {
        if (string.IsNullOrEmpty(_folderPath)) return;

        var filter  = string.IsNullOrWhiteSpace(TxtFilter.Text) ? "*.*" : TxtFilter.Text.Trim();
        var subDirs = ChkSubDir.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            var files = Directory.GetFiles(_folderPath, filter, subDirs);
            _items = files.Select(f =>
            {
                var fi = new FileInfo(f);
                return new FolderHashItem
                {
                    FilePath     = f,
                    RelativePath = Path.GetRelativePath(_folderPath, f),
                    FileSize     = fi.Length
                };
            }).ToList();

            LvFiles.ItemsSource = _items;
            TxtSummary.Text = $"{_items.Count}개 파일 발견. '해시 계산'을 클릭하세요.";
            BtnExport.IsEnabled = false;
            BtnSaveTxt.IsEnabled = false;
        }
        catch (Exception ex)
        {
            TxtSummary.Text = $"오류: {ex.Message}";
        }
    }

    // ── 해시 계산 ────────────────────────────────────────────────
    private async void BtnCompute_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0 || string.IsNullOrEmpty(_folderPath)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        BtnCompute.IsEnabled = false;

        // 파일 목록 재로드 (필터/옵션 변경 반영)
        LoadFileList();
        if (_items.Count == 0) { BtnCompute.IsEnabled = true; return; }

        var mw = Window.GetWindow(this) as MainWindow;
        mw?.ShowProgress(true, false);
        mw?.SetStatus($"해시 계산 중... (0/{_items.Count})");

        int done = 0, errors = 0;
        bool cancelOnError = ChkCancelOnError.IsChecked == true;

        foreach (var item in _items)
        {
            if (_cts.Token.IsCancellationRequested) break;
            try
            {
                item.Hash = await _svc.ComputeAsync(item.FilePath, _algo, ct: _cts.Token);
                item.HashColor = "#D4D4D4";
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                item.Hash = $"(오류: {ex.Message})";
                item.HashColor = "#EF4444";
                errors++;
                if (cancelOnError) break;
            }

            done++;
            mw?.ShowProgress(true, false, done, _items.Count);
            mw?.SetStatus($"해시 계산 중... ({done}/{_items.Count})");
        }

        // 리바인딩
        LvFiles.ItemsSource = null;
        LvFiles.ItemsSource = _items;

        var algoName = _algo switch
        {
            HashAlgorithmKind.MD5    => "MD5",
            HashAlgorithmKind.SHA1   => "SHA-1",
            HashAlgorithmKind.SHA256 => "SHA-256",
            HashAlgorithmKind.SHA512 => "SHA-512",
            _                        => ""
        };

        TxtSummary.Text = errors > 0
            ? $"{done}개 완료 ({algoName}), 오류 {errors}개"
            : $"{done}개 완료 ({algoName})";

        BtnExport.IsEnabled  = done > 0;
        BtnSaveTxt.IsEnabled = done > 0;
        BtnCompute.IsEnabled = true;
        mw?.ShowProgress(false);
        mw?.SetStatus(TxtSummary.Text);
    }

    // ── 내보내기 ─────────────────────────────────────────────────
    private void BtnSaveTxt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "해시 목록 저장 (GNU 형식)",
            Filter     = "텍스트 파일|*.txt|SHA-256 파일|*.sha256|MD5 파일|*.md5|모든 파일|*.*",
            DefaultExt = "txt",
            FileName   = $"hashes_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = _items
                .Where(i => !i.Hash.StartsWith('('))
                .Select(i => $"{i.Hash}  {i.RelativePath}");
            File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
            (Window.GetWindow(this) as MainWindow)?.SetStatus($"저장: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 오류:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "CSV 내보내기",
            Filter     = "CSV 파일|*.csv",
            DefaultExt = "csv",
            FileName   = $"folder_hashes_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var algoName = _algo switch
            {
                HashAlgorithmKind.MD5    => "MD5",
                HashAlgorithmKind.SHA1   => "SHA-1",
                HashAlgorithmKind.SHA256 => "SHA-256",
                HashAlgorithmKind.SHA512 => "SHA-512",
                _                        => ""
            };
            var lines = new List<string> { $"파일명,크기,{algoName},경로" };
            foreach (var item in _items)
                lines.Add($"{CsvEscape(item.FileName)},{item.SizeText},{item.Hash},{CsvEscape(item.RelativePath)}");
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
}
