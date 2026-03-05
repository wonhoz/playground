using Microsoft.Win32;

namespace EchoText.Views;

public partial class EditorView : UserControl
{
    private readonly TtsService _svc = new();
    private CancellationTokenSource? _cts;

    public EditorView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadVoices();
    }

    // ── 음성 목록 로드 ────────────────────────────────────────────
    private void LoadVoices()
    {
        var voices = _svc.GetVoices();
        CbVoice.ItemsSource   = voices;
        CbVoice.DisplayMemberPath = "Name";
        CbVoice.SelectedIndex = voices.Count > 0 ? 0 : -1;
    }

    private void CbVoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CbVoice.SelectedItem is not System.Speech.Synthesis.VoiceInfo vi) return;
        _svc.SelectVoice(vi.Name);
        TxtVoiceInfo.Text = $"{vi.Gender} · {vi.Age} · {vi.Culture.Name}";
        UpdateStats();
    }

    // ── 슬라이더 이벤트 ──────────────────────────────────────────
    private void SlRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int v = (int)SlRate.Value;
        TxtRate.Text = v.ToString("+0;-0;0");
        _svc.Rate = v;
        UpdateStats();
    }

    private void SlVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int v = (int)SlVolume.Value;
        TxtVolume.Text = v.ToString();
        _svc.Volume = v;
    }

    private void SlPitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        int v = (int)SlPitch.Value;
        TxtPitch.Text = v.ToString("+0;-0;0");
        _svc.Pitch = v;
    }

    // ── 텍스트 변경 ──────────────────────────────────────────────
    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateStats();
    }

    private void UpdateStats()
    {
        var text = TxtEditor?.Text ?? "";
        var (chars, words, est) = TtsService.Analyze(text, (int)(SlRate?.Value ?? 0));

        if (TxtCharCount != null) TxtCharCount.Text = $"글자 {chars:N0}";
        if (TxtWordCount != null) TxtWordCount.Text = $"단어 {words:N0}";
        if (TxtEstTime   != null) TxtEstTime.Text   = est.TotalSeconds < 60
            ? $"예상 {est.TotalSeconds:F0}초"
            : $"예상 {(int)est.TotalMinutes}분 {est.Seconds:D2}초";
    }

    // ── 재생 제어 ─────────────────────────────────────────────────
    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtEditor.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _svc.SsmlMode = ChkSsml.IsChecked == true;
        _svc.SpeakAsync(text);

        BtnPlay.IsEnabled = false;
        BtnStop.IsEnabled = true;
        (Window.GetWindow(this) as MainWindow)?.SetStatus("재생 중...");

        // 재생 완료 확인 타이머
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) =>
        {
            if (!_svc.IsSpeaking)
            {
                timer.Stop();
                BtnPlay.IsEnabled = true;
                BtnStop.IsEnabled = false;
                (Window.GetWindow(this) as MainWindow)?.SetStatus("준비");
            }
        };
        timer.Start();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _svc.Stop();
        _cts?.Cancel();
        BtnPlay.IsEnabled = true;
        BtnStop.IsEnabled = false;
        (Window.GetWindow(this) as MainWindow)?.SetStatus("중단됨");
    }

    private void BtnClipboard_Click(object sender, RoutedEventArgs e)
    {
        var clip = Clipboard.GetText().Trim();
        if (string.IsNullOrEmpty(clip)) return;
        TxtEditor.Text = clip;
        (Window.GetWindow(this) as MainWindow)?.SetStatus("클립보드에서 붙여넣기");
    }

    // ── 내보내기 ─────────────────────────────────────────────────
    private async void BtnSaveWav_Click(object sender, RoutedEventArgs e)
        => await ExportAsync(false);

    private async void BtnSaveMp3_Click(object sender, RoutedEventArgs e)
        => await ExportAsync(true);

    private async Task ExportAsync(bool toMp3)
    {
        var text = TxtEditor.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var ext    = toMp3 ? "mp3" : "wav";
        var filter = toMp3 ? "MP3 파일|*.mp3" : "WAV 파일|*.wav";

        var dlg = new SaveFileDialog
        {
            Title      = toMp3 ? "MP3로 저장" : "WAV로 저장",
            Filter     = filter,
            DefaultExt = ext,
            FileName   = $"echo_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
        };
        if (dlg.ShowDialog() != true) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _svc.SsmlMode = ChkSsml.IsChecked == true;
        BtnPlay.IsEnabled = false;
        PbExport.Visibility = Visibility.Visible;
        PbExport.Value = 0; PbExport.Maximum = 100;

        var mw = Window.GetWindow(this) as MainWindow;
        mw?.SetStatus(toMp3 ? "MP3 변환 중..." : "WAV 저장 중...");
        mw?.ShowProgress(true, false);

        var wavPath = toMp3 ? Path.GetTempFileName() + ".wav" : dlg.FileName;

        try
        {
            var prog = new Progress<int>(v =>
            {
                PbExport.Value = v;
                mw?.ShowProgress(true, false, v, 100);
            });

            var elapsed = await _svc.SpeakToWavAsync(text, wavPath, prog, _cts.Token);

            if (toMp3)
            {
                mw?.SetStatus("WAV → MP3 변환 중...");
                await ExportService.WavToMp3Async(wavPath, dlg.FileName);
                try { File.Delete(wavPath); } catch { }
            }

            mw?.SetStatus($"저장 완료: {Path.GetFileName(dlg.FileName)}  ({elapsed.TotalSeconds:F1}초 소요)");
        }
        catch (OperationCanceledException)
        {
            mw?.SetStatus("취소됨");
            try { File.Delete(wavPath); } catch { }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 오류:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            mw?.SetStatus($"오류: {ex.Message}");
        }
        finally
        {
            BtnPlay.IsEnabled   = true;
            PbExport.Visibility = Visibility.Collapsed;
            mw?.ShowProgress(false);
        }
    }

    // ── 옵션 ─────────────────────────────────────────────────────
    private void ChkSsml_Changed(object sender, RoutedEventArgs e)
    {
        _svc.SsmlMode = ChkSsml.IsChecked == true;
    }

    private void ChkWordWrap_Changed(object sender, RoutedEventArgs e)
    {
        TxtEditor.TextWrapping = ChkWordWrap.IsChecked == true
            ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }
}
