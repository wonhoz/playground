using System.Diagnostics;
using System.Security;
using System.Speech.Synthesis;

namespace EchoText.Services;

public class TtsService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();

    // ── 설정 프로퍼티 ─────────────────────────────────────────────
    public int  Rate    { get; set; } = 0;    // -10 ~ +10
    public int  Volume  { get; set; } = 100;  // 0 ~ 100
    public int  Pitch   { get; set; } = 0;    // -10 ~ +10 (semitones, SSML)
    public bool SsmlMode { get; set; } = false;

    // ── 음성 목록 ────────────────────────────────────────────────
    public IReadOnlyList<VoiceInfo> GetVoices()
        => _synth.GetInstalledVoices()
                 .Where(v => v.Enabled)
                 .Select(v => v.VoiceInfo)
                 .ToList();

    public void SelectVoice(string name)
    {
        try { _synth.SelectVoice(name); }
        catch { /* 음성 없으면 무시 */ }
    }

    public string CurrentVoiceName => _synth.Voice.Name;

    // ── 상태 ──────────────────────────────────────────────────────
    public bool IsSpeaking => _synth.State == SynthesizerState.Speaking;

    // ── 실시간 재생 ──────────────────────────────────────────────
    public void SpeakAsync(string text)
    {
        _synth.Rate   = Rate;
        _synth.Volume = Volume;
        _synth.SpeakAsyncCancelAll();
        _synth.SetOutputToDefaultAudioDevice();

        var ssml = SsmlMode ? EnsureSpeakTag(text) : BuildSsml(text);
        _synth.SpeakSsmlAsync(ssml);
    }

    public void Stop() => _synth.SpeakAsyncCancelAll();

    // ── WAV 파일 저장 ────────────────────────────────────────────
    public async Task<TimeSpan> SpeakToWavAsync(
        string text, string outputPath,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        _synth.Rate   = Rate;
        _synth.Volume = Volume;

        var sw  = Stopwatch.StartNew();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int totalChars = Math.Max(text.Length, 1);

        EventHandler<SpeakProgressEventArgs>? onProgress  = null;
        EventHandler<SpeakCompletedEventArgs>? onCompleted = null;

        onProgress = (_, e) =>
        {
            int pct = Math.Min(e.CharacterPosition * 100 / totalChars, 99);
            progress?.Report(pct);
        };

        onCompleted = (_, e) =>
        {
            _synth.SpeakProgress -= onProgress;
            _synth.SpeakCompleted -= onCompleted;

            if (e.Cancelled)      tcs.TrySetCanceled();
            else if (e.Error != null) tcs.TrySetException(e.Error);
            else                  tcs.TrySetResult(true);
        };

        _synth.SpeakProgress  += onProgress;
        _synth.SpeakCompleted += onCompleted;
        _synth.SetOutputToWaveFile(outputPath);

        try
        {
            var ssml = SsmlMode ? EnsureSpeakTag(text) : BuildSsml(text);
            _synth.SpeakSsmlAsync(ssml);

            using var reg = ct.Register(() => _synth.SpeakAsyncCancelAll());
            await tcs.Task;
            progress?.Report(100);
        }
        finally
        {
            _synth.SetOutputToDefaultAudioDevice();
        }

        sw.Stop();
        return sw.Elapsed;
    }

    // ── SSML 빌더 ────────────────────────────────────────────────
    private string BuildSsml(string text)
    {
        var pitchStr = Pitch > 0 ? $"+{Pitch}st" : Pitch < 0 ? $"{Pitch}st" : "+0st";
        var escaped  = SecurityElement.Escape(text) ?? text;

        return $"""
            <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="ko-KR">
              <prosody pitch="{pitchStr}">{escaped}</prosody>
            </speak>
            """;
    }

    private static string EnsureSpeakTag(string ssml)
    {
        if (ssml.TrimStart().StartsWith("<speak", StringComparison.OrdinalIgnoreCase))
            return ssml;

        return $"""
            <speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="ko-KR">
              {ssml}
            </speak>
            """;
    }

    // ── 텍스트 분석 ──────────────────────────────────────────────
    public static (int chars, int words, TimeSpan estimated) Analyze(string text, int rate)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (0, 0, TimeSpan.Zero);

        int chars = text.Length;
        int words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // 속도 비율 계산: rate -10 ~ +10, 0 = 약 180 WPM
        // rate: -10 = 0.5x, 0 = 1.0x, +10 = ~3.0x (대략적 추정)
        double speedMult = rate switch
        {
            <= -10 => 0.5,
            >= 10  => 3.0,
            _      => 1.0 + rate * (rate > 0 ? 0.2 : 0.05)
        };
        double wpm      = 180.0 * speedMult;
        double minutes  = words / wpm;
        var estimated   = TimeSpan.FromMinutes(minutes);

        return (chars, words, estimated);
    }

    public void Dispose()
    {
        _synth.Dispose();
        GC.SuppressFinalize(this);
    }
}
