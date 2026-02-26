using System.IO;
using System.Media;
using System.Windows.Media;

namespace HueFlow.Sound;

/// <summary>
/// 프로시저럴 오디오 합성 + 재생 엔진.
/// </summary>
public static class SoundGen
{
    private const int R = 22050;

    // ── 파형 생성 ────────────────────────────────

    /// <summary>wave: 0=sine, 1=square, 2=saw, 3=triangle</summary>
    public static byte[] Tone(double freq, double dur, double vol = 0.5, int wave = 0)
    {
        int n = (int)(R * dur);
        var d = new short[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / R;
            double v = wave switch
            {
                1 => Math.Sin(2 * Math.PI * freq * t) >= 0 ? 1.0 : -1.0,
                2 => 2.0 * ((t * freq) % 1.0) - 1.0,
                3 => Math.Abs(2.0 * ((t * freq) % 1.0) - 1.0) * 2 - 1,
                _ => Math.Sin(2 * Math.PI * freq * t)
            };
            double env = Env(i, n, 0.008, 0.015);
            d[i] = (short)(v * vol * 30000 * env);
        }
        return ToWav(d);
    }

    public static byte[] Noise(double dur, double vol = 0.3, double decay = 1.5)
    {
        var rng = new Random(42);
        int n = (int)(R * dur);
        var d = new short[n];
        for (int i = 0; i < n; i++)
        {
            double e = Math.Pow(1.0 - (double)i / n, decay);
            d[i] = (short)((rng.NextDouble() * 2 - 1) * vol * 30000 * e);
        }
        return ToWav(d);
    }

    public static byte[] Sweep(double f1, double f2, double dur, double vol = 0.4)
    {
        int n = (int)(R * dur);
        var d = new short[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double p = (double)i / n;
            double freq = f1 + (f2 - f1) * p;
            phase += 2 * Math.PI * freq / R;
            d[i] = (short)(Math.Sin(phase) * vol * 30000 * (1.0 - p * 0.5));
        }
        return ToWav(d);
    }

    public static byte[] Seq((double hz, double sec)[] notes, int wave = 1, double vol = 0.25)
    {
        var all = new List<short>();
        foreach (var (hz, sec) in notes)
        {
            int n = (int)(R * sec);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / R;
                double v = hz < 1 ? 0 : wave switch
                {
                    1 => Math.Sin(2 * Math.PI * hz * t) >= 0 ? 1.0 : -1.0,
                    2 => 2.0 * ((t * hz) % 1.0) - 1.0,
                    _ => Math.Sin(2 * Math.PI * hz * t)
                };
                double env = Env(i, n, 0.005, 0.02);
                all.Add((short)(v * vol * 30000 * env));
            }
        }
        return ToWav([.. all]);
    }

    public static byte[] Mix(params byte[][] wavs)
    {
        var tracks = wavs.Select(ExtractSamples).ToList();
        if (tracks.Count == 0) return ToWav([]);
        int max = tracks.Max(t => t.Length);
        if (max == 0) return ToWav([]);
        var m = new int[max];
        foreach (var tr in tracks)
            for (int i = 0; i < tr.Length; i++)
                m[i] += tr[i];
        var d = new short[max];
        for (int i = 0; i < max; i++)
            d[i] = (short)Math.Clamp(m[i], -30000, 30000);
        return ToWav(d);
    }

    public static byte[] Repeat(byte[] wav, int count)
    {
        var s = ExtractSamples(wav);
        var d = new short[s.Length * count];
        for (int t = 0; t < count; t++)
            Array.Copy(s, 0, d, t * s.Length, s.Length);
        return ToWav(d);
    }

    // ── 재생 ─────────────────────────────────────

    public static void Sfx(byte[] wav)
    {
        var p = new SoundPlayer(new MemoryStream(wav));
        p.Play();
    }

    private static MediaPlayer? _bgm;
    private static string? _bgmFile;

    public static void PlayBgm(byte[] wav, double volume = 0.35)
    {
        StopBgm();
        _bgmFile = Path.Combine(Path.GetTempPath(), $"bgm_{Guid.NewGuid():N}.wav");
        try { File.WriteAllBytes(_bgmFile, wav); }
        catch { _bgmFile = null; return; }
        _bgm = new MediaPlayer();
        _bgm.Open(new Uri(_bgmFile));
        _bgm.Volume = volume;
        var player = _bgm;
        _bgm.MediaEnded += (_, _) => { player.Position = TimeSpan.Zero; player.Play(); };
        _bgm.Play();
    }

    public static void StopBgm()
    {
        if (_bgm != null) { _bgm.Stop(); _bgm.Close(); _bgm = null; }
        if (_bgmFile != null) { try { File.Delete(_bgmFile); } catch { } _bgmFile = null; }
    }

    // ── 음표 주파수 ──────────────────────────────

    public const double
        C3 = 131, D3 = 147, Eb3 = 156, E3 = 165, F3 = 175, Gb3 = 185, G3 = 196, Ab3 = 208, A3 = 220, Bb3 = 233, B3 = 247,
        C4 = 262, D4 = 294, Eb4 = 311, E4 = 330, F4 = 349, Gb4 = 370, G4 = 392, Ab4 = 415, A4 = 440, Bb4 = 466, B4 = 494,
        C5 = 523, D5 = 587, Eb5 = 622, E5 = 659, F5 = 698, Gb5 = 740, G5 = 784, Ab5 = 831, A5 = 880, Bb5 = 932, B5 = 988,
        REST = 0;

    // ── 내부 ─────────────────────────────────────

    private static double Env(int i, int total, double att, double rel)
    {
        double a = Math.Min(1.0, (double)i / Math.Max(1, R * att));
        double r = Math.Min(1.0, (double)(total - i) / Math.Max(1, R * rel));
        return Math.Min(a, r);
    }

    private static byte[] ToWav(short[] d)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int sz = d.Length * 2;
        bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + sz);
        bw.Write(new[] { 'W', 'A', 'V', 'E' }); bw.Write(new[] { 'f', 'm', 't', ' ' });
        bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(R); bw.Write(R * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(sz);
        foreach (var s in d) bw.Write(s);
        return ms.ToArray();
    }

    private static short[] ExtractSamples(byte[] wav)
    {
        if (wav.Length < 44) return [];
        int n = (wav.Length - 44) / 2;
        var s = new short[n];
        Buffer.BlockCopy(wav, 44, s, 0, n * 2);
        return s;
    }
}
