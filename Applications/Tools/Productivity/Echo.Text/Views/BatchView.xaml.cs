using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace EchoText.Views;

public class ChapterItem
{
    public int    Index       { get; set; }
    public string Title       { get; set; } = "";
    public string Text        { get; set; } = "";
    public int    CharCount   => Text.Length;
    public string Status      { get; set; } = "대기";
    public string StatusColor { get; set; } = "#888899";
    public string OutputFile  { get; set; } = "";
}

public partial class BatchView : UserControl
{
    private readonly TtsService _svc = new();
    private readonly ObservableCollection<ChapterItem> _chapters = [];
    private CancellationTokenSource? _cts;
    private string? _outFolder;
    private string? _loadedText;

    public BatchView()
    {
        InitializeComponent();
        LvChapters.ItemsSource = _chapters;
        Loaded += (_, _) => LoadVoices();
    }

    // ── 음성 목록 ─────────────────────────────────────────────────
    private void LoadVoices()
    {
        var voices = _svc.GetVoices();
        CbVoice.ItemsSource        = voices;
        CbVoice.DisplayMemberPath  = "Name";
        CbVoice.SelectedIndex      = voices.Count > 0 ? 0 : -1;
    }

    private void CbVoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CbVoice.SelectedItem is not System.Speech.Synthesis.VoiceInfo vi) return;
        _svc.SelectVoice(vi.Name);
    }

    private void SlRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int v = (int)SlRate.Value;
        TxtRate.Text = v.ToString("+0;-0;0");
        _svc.Rate = v;
    }

    private void SlVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int v = (int)SlVolume.Value;
        TxtVolume.Text = v.ToString();
        _svc.Volume = v;
    }

    // ── 파일 로드 ─────────────────────────────────────────────────
    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "텍스트 파일 열기",
            Filter = "텍스트 파일|*.txt|모든 파일|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _loadedText = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            TxtFilePath.Text       = dlg.FileName;
            TxtFilePath.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
            BtnPreview.IsEnabled   = true;
            _chapters.Clear();
            TxtEmptyHint.Visibility = Visibility.Visible;
            (Window.GetWindow(this) as MainWindow)?.SetStatus($"로드: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 열기 오류:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 미리보기 분할 ─────────────────────────────────────────────
    private void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_loadedText)) return;

        var delimiter = TxtDelimiter.Text;
        if (string.IsNullOrEmpty(delimiter)) delimiter = "\n\n";

        var splits = ExportService.SplitChapters(_loadedText, delimiter);
        _chapters.Clear();

        for (int i = 0; i < splits.Count; i++)
        {
            var (title, text) = splits[i];
            _chapters.Add(new ChapterItem
            {
                Index      = i + 1,
                Title      = title,
                Text       = text,
                Status     = "대기",
                StatusColor = "#888899"
            });
        }

        UpdateOutputNames(false);
        TxtEmptyHint.Visibility = _chapters.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_chapters.Count == 0)
            (Window.GetWindow(this) as MainWindow)?.SetStatus("챕터를 찾을 수 없습니다. 구분자를 확인하세요.");
        else
        {
            UpdateBatchButtons();
            (Window.GetWindow(this) as MainWindow)?.SetStatus($"챕터 {_chapters.Count}개 분할 완료");
        }
    }

    private void UpdateOutputNames(bool mp3)
    {
        var ext = mp3 ? "mp3" : "wav";
        foreach (var ch in _chapters)
            ch.OutputFile = $"{ch.Index:D3}_{ExportService.SafeFileName(ch.Title[..Math.Min(30, ch.Title.Length)])}.{ext}";
    }

    // ── 출력 폴더 ─────────────────────────────────────────────────
    private void BtnPickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "출력 폴더 선택" };
        if (dlg.ShowDialog() != true) return;

        _outFolder = dlg.FolderName;
        TxtOutFolder.Text       = _outFolder;
        TxtOutFolder.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        UpdateBatchButtons();
    }

    private void UpdateBatchButtons()
    {
        bool ready = _chapters.Count > 0 && !string.IsNullOrEmpty(_outFolder);
        BtnBatchWav.IsEnabled = ready;
        BtnBatchMp3.IsEnabled = ready;
    }

    // ── 일괄 내보내기 ─────────────────────────────────────────────
    private void BtnBatchWav_Click(object sender, RoutedEventArgs e) => _ = BatchExportAsync(false);
    private void BtnBatchMp3_Click(object sender, RoutedEventArgs e) => _ = BatchExportAsync(true);

    private async Task BatchExportAsync(bool toMp3)
    {
        if (_chapters.Count == 0 || string.IsNullOrEmpty(_outFolder)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        BtnBatchWav.IsEnabled = false;
        BtnBatchMp3.IsEnabled = false;
        BtnPreview.IsEnabled  = false;
        PbBatch.Visibility    = Visibility.Visible;
        PbBatch.Maximum       = _chapters.Count;
        PbBatch.Value         = 0;

        var ext = toMp3 ? "mp3" : "wav";
        UpdateOutputNames(toMp3);

        var mw = Window.GetWindow(this) as MainWindow;
        mw?.ShowProgress(true, false);

        int done = 0, errors = 0;

        foreach (var ch in _chapters)
        {
            if (_cts.Token.IsCancellationRequested)
            {
                ch.Status      = "취소";
                ch.StatusColor = "#888899";
                continue;
            }

            ch.Status      = "변환 중...";
            ch.StatusColor = "#A855F7";
            RefreshList();

            var outPath = Path.Combine(_outFolder!, ch.OutputFile);
            var wavPath = toMp3 ? Path.GetTempFileName() + ".wav" : outPath;

            try
            {
                await _svc.SpeakToWavAsync(ch.Text, wavPath, ct: _cts.Token);

                if (toMp3)
                {
                    await ExportService.WavToMp3Async(wavPath, outPath);
                    try { File.Delete(wavPath); } catch { }
                }

                ch.Status      = "✔ 완료";
                ch.StatusColor = "#22C55E";
                done++;
            }
            catch (OperationCanceledException)
            {
                ch.Status      = "취소";
                ch.StatusColor = "#888899";
                try { if (toMp3) File.Delete(wavPath); } catch { }
            }
            catch (Exception ex)
            {
                ch.Status      = $"✘ 오류";
                ch.StatusColor = "#EF4444";
                ch.OutputFile  = ex.Message;
                errors++;
            }

            PbBatch.Value = ++done;
            mw?.ShowProgress(true, false, done, _chapters.Count);
            RefreshList();
        }

        BtnBatchWav.IsEnabled = true;
        BtnBatchMp3.IsEnabled = true;
        BtnPreview.IsEnabled  = true;
        PbBatch.Visibility    = Visibility.Collapsed;
        mw?.ShowProgress(false);
        mw?.SetStatus($"일괄 완료: {done}개 성공, {errors}개 오류");
    }

    private void RefreshList()
    {
        var copy = _chapters.ToList();
        _chapters.Clear();
        foreach (var item in copy) _chapters.Add(item);
    }
}
