using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace Geo.Quiz.Services;

/// <summary>
/// 배경음악(MediaPlayer 루프) + 효과음(WinMM PlaySound) 서비스.
/// WAV 데이터를 코드로 직접 생성하며 외부 파일·NuGet 불필요.
/// </summary>
public sealed class AudioService : IDisposable
{
    // ── WinMM PlaySound (효과음 비동기 재생) ───────────────────────────────
    [DllImport("winmm.dll")] static extern bool PlaySound(byte[]? data, IntPtr hmod, uint flags);
    const uint SND_ASYNC     = 0x0001;
    const uint SND_MEMORY    = 0x0004;
    const uint SND_NODEFAULT = 0x0002;
    const uint SND_PURGE     = 0x0040;

    // ── 배경음악 ───────────────────────────────────────────────────────────
    readonly MediaPlayer _bg = new();
    readonly string _bgFile;
    bool _disposed;

    // ── 효과음 (static: GC 방지) ───────────────────────────────────────────
    static readonly byte[] SfxCorrect = MakeTones(22050, (659.3, 0.55, 180), (783.99, 0.60, 230));
    static readonly byte[] SfxWrong   = MakeWrong();
    static readonly byte[] SfxClick   = MakeTones(22050, (1046.5, 0.30, 38));

    // ── 볼륨 설정 ──────────────────────────────────────────────────────────
    bool _muted;
    public bool Muted
    {
        get => _muted;
        set { _muted = value; _bg.Volume = value ? 0 : BgVol; }
    }
    const double BgVol = 0.28;

    public AudioService()
    {
        var wav = MakeAmbient();
        _bgFile = Path.Combine(Path.GetTempPath(), $"gq_bg_{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(_bgFile, wav);

        _bg.Open(new Uri(_bgFile, UriKind.Absolute));
        _bg.Volume = BgVol;
        _bg.MediaEnded += (_, _) => { _bg.Position = TimeSpan.Zero; _bg.Play(); };
    }

    // ── 공개 API ───────────────────────────────────────────────────────────
    public void PlayBg()        { if (!_muted) _bg.Play(); }
    public void PauseBg()       => _bg.Pause();
    public void PlayCorrect()   => Sfx(SfxCorrect);
    public void PlayWrong()     => Sfx(SfxWrong);
    public void PlayClick()     => Sfx(SfxClick);

    void Sfx(byte[] data)
    {
        if (_muted) return;
        PlaySound(data, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT);
    }

    // ── WAV 생성 ──────────────────────────────────────────────────────────
    /// <summary>8초 Am-F-C-G 코드 진행 앰비언트 루프</summary>
    static byte[] MakeAmbient(int sr = 44100)
    {
        // 각 코드: [chord tones..., bass]
        double[][] chords =
        [
            [220.0, 261.6, 329.6, 110.0],  // Am: A3 C4 E4 + A2
            [174.6, 220.0, 261.6,  87.3],  // F:  F3 A3 C4 + F2
            [261.6, 329.6, 392.0, 130.8],  // C:  C4 E4 G4 + C3
            [196.0, 246.9, 293.7,  98.0],  // G:  G3 B3 D4 + G2
        ];

        int secLen  = sr * 2;
        int fadeLen = sr / 5;   // 200ms
        float[] buf = new float[sr * 8];

        for (int s = 0; s < 4; s++)
        {
            var f   = chords[s];
            int off = s * secLen;
            for (int i = 0; i < secLen; i++)
            {
                double t = (double)i / sr;
                double v = f[0..3].Sum(hz => 0.11 * Math.Sin(2 * Math.PI * hz * t));
                v += 0.07 * Math.Sin(2 * Math.PI * f[3] * t);       // bass
                v += 0.03 * Math.Sin(2 * Math.PI * f[0] * 2 * t);   // harmonic

                double env = 1.0;
                if (i < fadeLen)          env = (double)i / fadeLen;
                if (i > secLen - fadeLen) env = (double)(secLen - i) / fadeLen;

                buf[off + i] += (float)(v * env);
            }
        }

        return ToWav(buf.Select(f => (short)Math.Clamp(f * 20000, -32767, 32767)).ToArray(), sr);
    }

    /// <summary>복수 톤 순차 연결 (정답음 / 클릭음)</summary>
    static byte[] MakeTones(int sr, params (double freq, double amp, int ms)[] tones)
    {
        int total = tones.Sum(t => t.ms) * sr / 1000 + 1;
        var pcm   = new short[total];
        int pos   = 0;
        foreach (var (freq, amp, ms) in tones)
        {
            int len = ms * sr / 1000;
            for (int i = 0; i < len; i++)
            {
                double t   = (double)i / sr;
                double env = Math.Pow(1.0 - (double)i / len, 0.35);
                if (i < sr / 200) env *= (double)i / (sr / 200); // 5ms fade-in
                pcm[pos + i] = (short)(Math.Sin(2 * Math.PI * freq * t) * env * amp * 28000);
            }
            pos += len;
        }
        return ToWav(pcm, sr);
    }

    /// <summary>오답음: Bb2 디튠 버즈 320ms 감쇠</summary>
    static byte[] MakeWrong(int sr = 22050)
    {
        int len = sr * 320 / 1000;
        var pcm = new short[len];
        for (int i = 0; i < len; i++)
        {
            double t   = (double)i / sr;
            double env = Math.Pow(1.0 - (double)i / len, 1.4);
            double v   = 0.50 * Math.Sin(2 * Math.PI * 116.5 * t)
                       + 0.30 * Math.Sin(2 * Math.PI * 120.3 * t)
                       + 0.15 * Math.Sin(2 * Math.PI * 233.0 * t);
            pcm[i] = (short)(v * env * 26000);
        }
        return ToWav(pcm, sr);
    }

    /// <summary>PCM short[] → WAV 바이트 배열 (모노 16-bit PCM)</summary>
    static byte[] ToWav(short[] pcm, int sr)
    {
        using var ms = new MemoryStream(44 + pcm.Length * 2);
        using var bw = new BinaryWriter(ms);
        int dataBytes = pcm.Length * 2;
        bw.Write("RIFF"u8); bw.Write(36 + dataBytes);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write("data"u8); bw.Write(dataBytes);
        foreach (var s in pcm) bw.Write(s);
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bg.Close();
        PlaySound(null, IntPtr.Zero, SND_PURGE);
        try { if (File.Exists(_bgFile)) File.Delete(_bgFile); } catch { }
    }
}
