namespace SoundBoard.Services;

/// <summary>NAudio RawSourceWaveStream 용 16-bit Mono 44100Hz PCM 바이트를 합성합니다.</summary>
public static class SoundSynthesizer
{
    private const int SR = 44100;

    public static readonly IReadOnlyDictionary<string, (string Name, string Emoji)> BuiltIns =
        new Dictionary<string, (string, string)>
        {
            ["airhorn"]  = ("Air Horn",     "📯"),
            ["applause"] = ("Applause",     "👏"),
            ["rimshot"]  = ("Rimshot",      "🥁"),
            ["sad"]      = ("Sad Trombone", "😢"),
            ["ding"]     = ("Ding",         "🔔"),
            ["laser"]    = ("Laser",        "⚡"),
            ["boom"]     = ("Boom",         "💥"),
            ["fanfare"]  = ("Fanfare",      "🎺"),
        };

    public static byte[] Generate(string key) => key switch
    {
        "airhorn"  => AirHorn(),
        "applause" => Applause(),
        "rimshot"  => Rimshot(),
        "sad"      => SadTrombone(),
        "ding"     => Ding(),
        "laser"    => Laser(),
        "boom"     => Boom(),
        "fanfare"  => Fanfare(),
        _          => Ding()
    };

    // ── 내장 사운드 합성 ──────────────────────────────────────────────────────

    private static byte[] AirHorn()
    {
        // 하모닉스 포함한 저음 경적 (2초)
        double dur = 2.0;
        var buf = new double[Len(dur)];
        for (int i = 0; i < buf.Length; i++)
        {
            double t = T(i);
            double f = 230 + 50 * Math.Exp(-t * 0.4);
            double env = Fade(t, 0.05) * Math.Exp(-t * 0.25);
            buf[i] = env * (0.5 * Sin(f, t) + 0.3 * Sin(f * 2, t) +
                           0.15 * Sin(f * 3, t) + 0.05 * Sin(f * 4, t));
        }
        return ToPcm(buf);
    }

    private static byte[] Applause()
    {
        // 군중 박수 (노이즈 + 8Hz 리듬 변조, 3초)
        double dur = 3.0;
        var buf = new double[Len(dur)];
        var rng = new Random(1234);
        for (int i = 0; i < buf.Length; i++)
        {
            double t = T(i);
            double env = Fade(t, 0.1) * Math.Min(1.0, (dur - t) * 1.5);
            double rhythm = 0.55 + 0.45 * Math.Sin(2 * Math.PI * 7.5 * t);
            buf[i] = (rng.NextDouble() * 2 - 1) * env * rhythm * 0.6;
        }
        return ToPcm(buf);
    }

    private static byte[] Rimshot()
    {
        // ba - dum - tss (1.5초)
        double dur = 1.5;
        var buf = new double[Len(dur)];
        void Hit(double start, double freq, double len, double amp)
        {
            int s = (int)(start * SR), e = Math.Min(buf.Length, s + (int)(len * SR));
            for (int i = s; i < e; i++)
            {
                double t = (double)(i - s) / SR;
                double env = Math.Exp(-t * (freq > 500 ? 4 : 18));
                buf[i] += amp * env * Sin(freq, t);
            }
        }
        Hit(0.00, 100, 0.18, 0.9);   // ba  (kick)
        Hit(0.25, 180, 0.14, 0.8);   // dum (snare body)
        Hit(0.25, 320, 0.14, 0.4);   // dum (snare snap)
        Hit(0.50, 900, 0.90, 0.35);  // tss (hi-hat)
        return ToPcm(buf);
    }

    private static byte[] SadTrombone()
    {
        // wah-wah-wah-wahhh 하강 (F4→Eb4→D4→B3, 2.5초)
        double dur = 2.5;
        var buf = new double[Len(dur)];
        double[] freqs = [349.23, 311.13, 293.66, 246.94];
        double[] starts = [0.0, 0.52, 1.0, 1.45];
        double[] lengths = [0.55, 0.55, 0.55, 1.05];

        for (int n = 0; n < freqs.Length; n++)
        {
            int s = (int)(starts[n] * SR);
            int e = Math.Min(buf.Length, s + (int)(lengths[n] * SR));
            double f = freqs[n];
            bool last = n == 3;
            for (int i = s; i < e; i++)
            {
                double t = (double)(i - s) / SR;
                double env = Fade(t, 0.06) * Math.Exp(-t * (last ? 2.0 : 1.5));
                buf[i] += env * 0.55 * (0.5 * Sin(f, t) + 0.3 * Sin(f * 2, t) + 0.15 * Sin(f * 3, t));
            }
        }
        return ToPcm(buf);
    }

    private static byte[] Ding()
    {
        // 맑은 벨 음 (880Hz, 2초 감쇠)
        double dur = 2.0;
        var buf = new double[Len(dur)];
        for (int i = 0; i < buf.Length; i++)
        {
            double t = T(i);
            double env = Math.Exp(-t * 2.2);
            buf[i] = env * (0.65 * Sin(880, t) + 0.2 * Sin(1760, t) + 0.05 * Sin(3520, t));
        }
        return ToPcm(buf);
    }

    private static byte[] Laser()
    {
        // sci-fi 레이저 (2000→150Hz 주파수 스윕, 0.7초)
        double dur = 0.7;
        var buf = new double[Len(dur)];
        for (int i = 0; i < buf.Length; i++)
        {
            double t = T(i);
            double f = 2000 * Math.Pow(150.0 / 2000.0, t / dur);
            double env = Math.Exp(-t * 1.5);
            buf[i] = env * Sin(f, t) * 0.75;
        }
        return ToPcm(buf);
    }

    private static byte[] Boom()
    {
        // 저주파 폭발음 (80→30Hz + 노이즈, 1.2초)
        double dur = 1.2;
        var buf = new double[Len(dur)];
        var rng = new Random(99);
        for (int i = 0; i < buf.Length; i++)
        {
            double t = T(i);
            double f = 80 * Math.Pow(0.35, t / dur);
            double env = Math.Exp(-t * 3.5);
            double noise = rng.NextDouble() * 2 - 1;
            buf[i] = env * (0.7 * Sin(f, t) + 0.3 * noise);
        }
        return ToPcm(buf);
    }

    private static byte[] Fanfare()
    {
        // 짧은 팡파르 C4→E4→G4→C5 (1.8초)
        double dur = 1.8;
        var buf = new double[Len(dur)];
        double[] freqs = [261.63, 329.63, 392.00, 523.25];
        double[] starts = [0.0, 0.28, 0.54, 0.80];
        double[] lengths = [0.32, 0.32, 0.32, 0.90];

        for (int n = 0; n < freqs.Length; n++)
        {
            int s = (int)(starts[n] * SR);
            int e = Math.Min(buf.Length, s + (int)(lengths[n] * SR));
            double f = freqs[n];
            bool last = n == 3;
            for (int i = s; i < e; i++)
            {
                double t = (double)(i - s) / SR;
                double env = Fade(t, 0.02) * Math.Exp(-t * (last ? 1.8 : 4.5));
                buf[i] += env * 0.45 * (0.5 * Sin(f, t) + 0.3 * Sin(f * 2, t) + 0.15 * Sin(f * 3, t));
            }
        }
        return ToPcm(buf);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────────

    private static int    Len(double secs) => (int)(SR * secs);
    private static double T(int i)         => (double)i / SR;
    private static double Sin(double f, double t) => Math.Sin(2 * Math.PI * f * t);
    /// <summary>0 ~ fadeSecs 구간에서 선형 페이드인.</summary>
    private static double Fade(double t, double fadeSecs) => Math.Min(1.0, t / fadeSecs);

    private static byte[] ToPcm(double[] buf, double peak = 0.85)
    {
        double max = 0;
        foreach (var v in buf) max = Math.Max(max, Math.Abs(v));
        double scale = max < 1e-9 ? 1.0 : peak / max;

        var samples = new short[buf.Length];
        for (int i = 0; i < buf.Length; i++)
            samples[i] = (short)Math.Max(-32767, Math.Min(32767, buf[i] * scale * 32767));

        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
