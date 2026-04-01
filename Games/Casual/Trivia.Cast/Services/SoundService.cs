using System.Media;
using System.Windows.Media;
using System.Windows.Threading;

namespace TriviaCast.Services;

public enum WaveForm { Sine, Square, Triangle }

/// <summary>
/// BGM: System.Windows.Media.MediaPlayer (Media Foundation — 별도 오디오 스택)
/// SFX: System.Media.SoundPlayer (WinMM)
/// 두 스택이 독립적이므로 동시 재생 가능
/// </summary>
public class SoundService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private MediaPlayer? _bgmPlayer;
    private string? _bgmFilePath;
    private bool _disposed;

    public bool BgmEnabled { get; set; } = true;
    public bool SfxEnabled { get; set; } = true;

    public SoundService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    // ─── BGM (MediaPlayer) ─────────────────────────────────────────────────

    public void StartBgm()
    {
        StopBgm();
        if (!BgmEnabled) return;
        _dispatcher.InvokeAsync(() =>
        {
            _bgmFilePath ??= WriteBgmFile();
            _bgmPlayer = new MediaPlayer { Volume = 0.28 };
            _bgmPlayer.MediaEnded += OnBgmEnded;
            _bgmPlayer.Open(new Uri(_bgmFilePath));
            _bgmPlayer.Play();
        });
    }

    public void StopBgm()
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (_bgmPlayer == null) return;
            _bgmPlayer.MediaEnded -= OnBgmEnded;
            _bgmPlayer.Stop();
            _bgmPlayer.Close();
            _bgmPlayer = null;
        });
    }

    private void OnBgmEnded(object? sender, EventArgs e)
    {
        if (_bgmPlayer == null || !BgmEnabled) return;
        _bgmPlayer.Position = TimeSpan.Zero;
        _bgmPlayer.Play();
    }

    private string WriteBgmFile()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Playground", "Trivia.Cast");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "bgm.wav");
        File.WriteAllBytes(path, BuildBgm());
        return path;
    }

    // ─── SFX (SoundPlayer / WinMM) ────────────────────────────────────────

    public void PlayCorrect()      => PlaySfx(ref _correct,  BuildCorrect);
    public void PlayWrong()        => PlaySfx(ref _wrong,    BuildWrong);
    public void PlayTimeout()      => PlaySfx(ref _timeout,  BuildTimeout);
    public void PlayClick()        => PlaySfx(ref _click,    BuildClick);
    public void PlayStreak()       => PlaySfx(ref _streak,   BuildStreak);
    public void PlayGameComplete() => PlaySfx(ref _complete, BuildGameComplete);
    public void PlayTimerTick()    => PlaySfx(ref _tick,     BuildTimerTick);

    private byte[]? _correct, _wrong, _timeout, _click, _streak, _complete, _tick;

    private void PlaySfx(ref byte[]? cache, Func<byte[]> builder)
    {
        if (!SfxEnabled) return;
        var data = cache ??= builder();
        Task.Run(() =>
        {
            using var ms = new MemoryStream(data);
            using var player = new SoundPlayer(ms);
            try { player.PlaySync(); } catch { }
        });
    }

    // ─── WAV 생성 ──────────────────────────────────────────────────────────

    private static byte[] Wav(
        (double freq, double dur, double vol)[] notes,
        int sr = 22050,
        WaveForm form = WaveForm.Sine)
    {
        int total = (int)(notes.Sum(n => n.dur) * sr);
        var pcm = new short[total];
        int pos = 0;
        foreach (var (freq, dur, vol) in notes)
        {
            int n = (int)(dur * sr);
            int att = Math.Min(n / 8, 200);
            int rel = Math.Min(n / 4, 600);
            for (int i = 0; i < n && pos < total; i++, pos++)
            {
                double t = (double)i / sr;
                double env = i < att ? (double)i / att :
                             i > n - rel ? (double)(n - i) / rel : 1.0;
                double raw = freq <= 0 ? 0 : form switch
                {
                    WaveForm.Square   => Math.Sign(Math.Sin(2 * Math.PI * freq * t)),
                    WaveForm.Triangle => 2 * Math.Abs(2 * (t * freq - Math.Floor(t * freq + 0.5))) - 1,
                    _                 => Math.Sin(2 * Math.PI * freq * t),
                };
                pcm[pos] = (short)(raw * vol * env * 28000);
            }
        }
        return ToWav(pcm, sr);
    }

    private static byte[] ToWav(short[] pcm, int sr)
    {
        int dataSize = pcm.Length * 2;
        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + dataSize); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataSize);
        foreach (var s in pcm) w.Write(s);
        return ms.ToArray();
    }

    // ─── 음표 주파수 ───────────────────────────────────────────────────────

    private const double C4  = 261.63, D4  = 293.66, E4  = 329.63, F4  = 349.23;
    private const double G4  = 392.00, A4  = 440.00, B4  = 493.88;
    private const double Ab4 = 415.30;
    private const double C5  = 523.25, D5  = 587.33, E5  = 659.26, G5 = 783.99, A5 = 880.00;

    // ─── BGM: C장조 아르페지오 멜로디 (~8초 루프) ─────────────────────────

    private static byte[] BuildBgm()
    {
        double b = 60.0 / 90 / 2; // 8분음표 @BPM 90
        return Wav([
            (E4, b, 0.20), (G4, b, 0.20), (A4, b, 0.20), (G4, b, 0.20),
            (E4, b, 0.20), (C4, b, 0.20), (D4, b, 0.20), (E4, b, 0.20),
            (F4, b, 0.20), (A4, b, 0.20), (C5, b, 0.20), (A4, b, 0.20),
            (F4, b, 0.20), (D4, b, 0.20), (E4, b, 0.20), (F4, b, 0.20),
            (G4, b, 0.20), (B4, b, 0.20), (D5, b, 0.20), (B4, b, 0.20),
            (G4, b, 0.20), (E4, b, 0.20), (F4, b, 0.20), (G4, b, 0.20),
            (A4, b, 0.20), (C5, b, 0.20), (E5, b, 0.20), (C5, b, 0.20),
            (A4, b, 0.20), (G4, b, 0.20), (F4, b, 0.20), (E4, b * 2, 0.20),
        ]);
    }

    // ─── 효과음 빌더 ───────────────────────────────────────────────────────

    private static byte[] BuildCorrect() => Wav([
        (G4, 0.10, 0.50), (0, 0.02, 0),
        (C5, 0.22, 0.55),
    ]);

    private static byte[] BuildWrong() => Wav([
        (E4, 0.10, 0.50), (0, 0.02, 0),
        (Ab4, 0.28, 0.45),
    ]);

    private static byte[] BuildTimeout() => Wav([
        (D4, 0.13, 0.50), (0, 0.05, 0),
        (D4, 0.13, 0.40),
    ], form: WaveForm.Square);

    private static byte[] BuildClick() => Wav([
        (C5, 0.055, 0.28),
    ]);

    private static byte[] BuildStreak() => Wav([
        (C5, 0.09, 0.42), (0, 0.01, 0),
        (E5, 0.09, 0.44), (0, 0.01, 0),
        (G5, 0.20, 0.48),
    ]);

    private static byte[] BuildGameComplete() => Wav([
        (C4, 0.10, 0.50), (0, 0.03, 0),
        (C4, 0.10, 0.50), (0, 0.03, 0),
        (C4, 0.10, 0.50), (0, 0.03, 0),
        (C4, 0.20, 0.50), (0, 0.06, 0),
        (E4, 0.12, 0.50), (G4, 0.12, 0.50),
        (C5, 0.45, 0.58),
    ]);

    private static byte[] BuildTimerTick() => Wav([
        (A5, 0.045, 0.18),
    ]);

    // ─── Dispose ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_disposed)
        {
            StopBgm();
            _disposed = true;
        }
    }
}
