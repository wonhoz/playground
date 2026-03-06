using HashCheck.Services;
using Microsoft.Win32;

namespace HashCheck.Views;

public partial class SingleView : UserControl
{
    private readonly HashService _svc = new();
    private CancellationTokenSource? _cts;
    private FileHashResult? _result;

    public SingleView()
    {
        InitializeComponent();
    }

    // ── 파일 처리 ─────────────────────────────────────────────────
    private async void LoadFile(string path)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        // UI 업데이트
        TxtFileName.Text = Path.GetFileName(path);
        var fi = new FileInfo(path);
        TxtFileMeta.Text = $"{FormatSize(fi.Length)}  •  {fi.LastWriteTime:yyyy-MM-dd HH:mm}  •  {path}";
        DropHint.Visibility = Visibility.Collapsed;
        FileInfo.Visibility = Visibility.Visible;

        SetHashTexts("계산 중...");
        PbHash.Visibility = Visibility.Visible;
        PbHash.IsIndeterminate = false;
        ClearCompare();

        var mw = Window.GetWindow(this) as MainWindow;
        mw?.SetStatus($"계산 중: {Path.GetFileName(path)}");

        try
        {
            var prog = new Progress<int>(v => { PbHash.Value = v; PbHash.Maximum = 100; });
            _result = await _svc.ComputeAllAsync(path, prog, _cts.Token);
            TxtMD5.Text    = _result.Hashes[HashAlgorithmKind.MD5];
            TxtSHA1.Text   = _result.Hashes[HashAlgorithmKind.SHA1];
            TxtSHA256.Text = _result.Hashes[HashAlgorithmKind.SHA256];
            TxtSHA512.Text = _result.Hashes[HashAlgorithmKind.SHA512];
            mw?.SetStatus($"완료  {_result.Elapsed.TotalSeconds:F2}초");
            TryCompare();
        }
        catch (OperationCanceledException) { SetHashTexts("취소됨"); }
        catch (Exception ex)
        {
            SetHashTexts("오류");
            mw?.SetStatus($"오류: {ex.Message}");
        }
        finally
        {
            PbHash.Visibility = Visibility.Collapsed;
        }
    }

    private void SetHashTexts(string val)
    {
        TxtMD5.Text = TxtSHA1.Text = TxtSHA256.Text = TxtSHA512.Text = val;
    }

    private void ClearFile_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _result = null;
        DropHint.Visibility = Visibility.Visible;
        FileInfo.Visibility = Visibility.Collapsed;
        SetHashTexts("—");
        TxtExpected.Text = "";
        ClearCompare();
        ResetCardBorders();
        (Window.GetWindow(this) as MainWindow)?.SetStatus("준비");
    }

    // ── 비교 ──────────────────────────────────────────────────────
    private void TxtExpected_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        TryCompare();
    }

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        TxtExpected.Text = Clipboard.GetText().Trim();
    }

    private void TryCompare()
    {
        var expected = TxtExpected.Text.Trim();
        if (string.IsNullOrEmpty(expected) || _result is null) { ClearCompare(); return; }

        // 해시 길이로 자동 감지
        string? actual = expected.Length switch
        {
            32  => _result.Hashes[HashAlgorithmKind.MD5],
            40  => _result.Hashes[HashAlgorithmKind.SHA1],
            64  => _result.Hashes[HashAlgorithmKind.SHA256],
            128 => _result.Hashes[HashAlgorithmKind.SHA512],
            _   => null
        };

        if (actual is null || actual == "계산 중..." || actual == "—")
        {
            ClearCompare();
            return;
        }

        bool match = HashService.HashEquals(expected, actual);
        string algoName = expected.Length switch
        {
            32 => "MD5", 40 => "SHA-1", 64 => "SHA-256", 128 => "SHA-512", _ => ""
        };

        CompareResult.Visibility = Visibility.Visible;
        TxtCompareIcon.Text = match ? "✔" : "✘";
        TxtCompareMsg.Text  = match
            ? $"일치 ({algoName})"
            : $"불일치 ({algoName})";

        var matchColor  = Color.FromRgb(0x22, 0xC5, 0x5E);
        var mismatch    = Color.FromRgb(0xEF, 0x44, 0x44);
        var color = match ? matchColor : mismatch;
        CompareResult.Background   = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));
        CompareResult.BorderBrush  = new SolidColorBrush(color);
        CompareResult.BorderThickness = new Thickness(1);
        TxtCompareIcon.Foreground  = new SolidColorBrush(color);
        TxtCompareMsg.Foreground   = new SolidColorBrush(color);

        // 해당 카드 테두리 강조
        ResetCardBorders();
        var card = expected.Length switch
        {
            32  => CardMD5,
            40  => CardSHA1,
            64  => CardSHA256,
            128 => CardSHA512,
            _   => null
        };
        if (card != null)
            card.BorderBrush = new SolidColorBrush(color);
    }

    private void ClearCompare() => CompareResult.Visibility = Visibility.Collapsed;

    private void ResetCardBorders()
    {
        var defaultBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E));
        foreach (var card in new[] { CardMD5, CardSHA1, CardSHA256, CardSHA512 })
            card.BorderBrush = defaultBrush;
    }

    // ── 복사 버튼 ─────────────────────────────────────────────────
    private void CopyMD5_Click(object sender, RoutedEventArgs e)    => Copy(TxtMD5.Text);
    private void CopySHA1_Click(object sender, RoutedEventArgs e)   => Copy(TxtSHA1.Text);
    private void CopySHA256_Click(object sender, RoutedEventArgs e) => Copy(TxtSHA256.Text);
    private void CopySHA512_Click(object sender, RoutedEventArgs e) => Copy(TxtSHA512.Text);

    private static void Copy(string text)
    {
        if (text is not ("—" or "계산 중..." or "오류" or "취소됨"))
            Clipboard.SetText(text);
    }

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
            LoadFile(files[0]);
    }

    private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "파일 선택", Filter = "모든 파일|*.*" };
        if (dlg.ShowDialog() == true) LoadFile(dlg.FileName);
    }

    private static string FormatSize(long b) => b switch
    {
        >= 1_073_741_824 => $"{b / 1_073_741_824.0:F2} GB",
        >= 1_048_576     => $"{b / 1_048_576.0:F2} MB",
        >= 1_024         => $"{b / 1024.0:F1} KB",
        _                => $"{b} B"
    };
}
