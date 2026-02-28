using System.IO;
using System.Media;

namespace NeonSlice.Services;

public enum SoundCue { Slice, SliceFever, Bomb, Lightning, Ice, Star, Miss, Fever, GameOver }

/// <summary>PCM WAV 코드 합성 기반 사운드 서비스 (외부 파일 불필요)</summary>
public sealed class SoundService : IDisposable
{
    private const int SR = 44100;
    private static readonly Random Rng = new();

    private readonly SoundPlayer   _bgm;
    private readonly SoundPlayer[] _slicePool = new SoundPlayer[3];
    private readonly SoundPlayer[] _feverPool = new SoundPlayer[3];
    private int  _sliceIdx;
    private int  _feverIdx;
    private readonly SoundPlayer _bomb;
    private readonly SoundPlayer _lightning;
    private readonly SoundPlayer _ice;
    private readonly SoundPlayer _star;
    private readonly SoundPlayer _miss;
    private readonly SoundPlayer _fever;
    private readonly SoundPlayer _gameOver;
    private bool _bgmPlaying;

    public SoundService()
    {
        _bgm = Make(GenerateBgm());
        for (var i = 0; i < 3; i++)
        {
            _slicePool[i] = Make(GenerateSlice(false));
            _feverPool[i] = Make(GenerateSlice(true));
        }
        _bomb      = Make(GenerateBomb());
        _lightning = Make(GenerateLightning());
        _ice       = Make(GenerateIce());
        _star      = Make(GenerateStar());
        _miss      = Make(GenerateMiss());
        _fever     = Make(GenerateFever());
        _gameOver  = Make(GenerateGameOver());
    }

    private static SoundPlayer Make(byte[] wav)
    {
        var p = new SoundPlayer(new MemoryStream(wav));
        p.Load();
        return p;
    }

    public void StartBgm()
    {
        if (_bgmPlaying) return;
        _bgm.PlayLooping();
        _bgmPlaying = true;
    }

    public void StopBgm()
    {
        _bgm.Stop();
        _bgmPlaying = false;
    }

    public void Play(SoundCue cue)
    {
        switch (cue)
        {
            case SoundCue.Slice:      _slicePool[_sliceIdx++ % 3].Play(); break;
            case SoundCue.SliceFever: _feverPool[_feverIdx++ % 3].Play(); break;
            case SoundCue.Bomb:       _bomb.Play();      break;
            case SoundCue.Lightning:  _lightning.Play(); break;
            case SoundCue.Ice:        _ice.Play();       break;
            case SoundCue.Star:       _star.Play();      break;
            case SoundCue.Miss:       _miss.Play();      break;
            case SoundCue.Fever:      _fever.Play();     break;
            case SoundCue.GameOver:   StopBgm(); _gameOver.Play(); break;
        }
    }

    public void Dispose()
    {
        _bgm.Dispose();
        foreach (var p in _slicePool) p.Dispose();
        foreach (var p in _feverPool) p.Dispose();
        _bomb.Dispose(); _lightning.Dispose(); _ice.Dispose();
        _star.Dispose(); _miss.Dispose(); _fever.Dispose(); _gameOver.Dispose();
    }

    // ── WAV 빌더 ─────────────────────────────────────────────────────────────

    private static byte[] BuildWav(short[] samples)
    {
        var dataSize = samples.Length * 2;
        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true);

        bw.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        bw.Write(36 + dataSize);
        bw.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
        bw.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(SR); bw.Write(SR * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        bw.Write(dataSize);
        foreach (var s in samples) bw.Write(s);
        bw.Flush();
        return ms.ToArray();
    }

    private static short Clip(double v)
        => (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)v));

    private static short[] Norm(double[] buf, double target = 28000)
    {
        double max = 0;
        foreach (var v in buf) { var a = Math.Abs(v); if (a > max) max = a; }
        var scale = max > 0 ? target / max : 0;
        var r = new short[buf.Length];
        for (var i = 0; i < buf.Length; i++) r[i] = Clip(buf[i] * scale);
        return r;
    }

    // ── 효과음 생성 ──────────────────────────────────────────────────────────

    /// <summary>슬라이스 — 고주파→저주파 스윕 (피버 시 더 높고 빠름)</summary>
    private static byte[] GenerateSlice(bool fever)
    {
        const double dur = 0.18;
        var n = (int)(dur * SR);
        var buf = new double[n];
        var phase = 0.0;
        var startF = fever ? 1400.0 : 900.0;
        var endF   = fever ? 800.0  : 300.0;

        for (var i = 0; i < n; i++)
        {
            var t     = (double)i / SR;
            var tNorm = (double)i / n;
            phase += 2 * Math.PI * (startF * Math.Pow(endF / startF, tNorm)) / SR;
            var env = t < 0.008 ? t / 0.008 : Math.Exp(-(t - 0.008) * 22);
            buf[i]  = (Math.Sin(phase) * 0.72 + (Rng.NextDouble() * 2 - 1) * 0.28) * env;
        }
        return BuildWav(Norm(buf));
    }

    /// <summary>폭탄 — 저음 킥 + 노이즈 버스트</summary>
    private static byte[] GenerateBomb()
    {
        const double dur = 0.55;
        var n = (int)(dur * SR);
        var buf = new double[n];
        var phase = 0.0;

        for (var i = 0; i < n; i++)
        {
            var t  = (double)i / SR;
            phase += 2 * Math.PI * (85 * Math.Exp(-t * 22)) / SR;
            buf[i] = Math.Sin(phase) * Math.Exp(-t * 7) * 0.85
                   + (Rng.NextDouble() * 2 - 1) * Math.Exp(-t * 40) * 0.45;
        }
        return BuildWav(Norm(buf, 30000));
    }

    /// <summary>번개 — 전기 버즈 (톱니파 + 트레몰로 + 노이즈)</summary>
    private static byte[] GenerateLightning()
    {
        const double dur = 0.35;
        var n = (int)(dur * SR);
        var buf = new double[n];
        var phase = 0.0;

        for (var i = 0; i < n; i++)
        {
            var t       = (double)i / SR;
            phase      += 2 * Math.PI * (500 * Math.Exp(-t * 4) + 150) / SR;
            var tremolo = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 50 * t);
            var saw     = 2 * ((phase % (2 * Math.PI)) / (2 * Math.PI)) - 1;
            buf[i]      = (saw * tremolo * 0.65
                          + (Rng.NextDouble() * 2 - 1) * Math.Exp(-t * 20) * 0.4)
                        * Math.Exp(-t * 9);
        }
        return BuildWav(Norm(buf));
    }

    /// <summary>얼음 — 크리스탈 벨 (배음 사인파 합성, 지수 감쇠)</summary>
    private static byte[] GenerateIce()
    {
        const double dur = 0.65;
        var n = (int)(dur * SR);
        var buf = new double[n];
        double p1 = 0, p2 = 0, p3 = 0;

        for (var i = 0; i < n; i++)
        {
            var t  = (double)i / SR;
            p1 += 2 * Math.PI * 950.0  / SR;
            p2 += 2 * Math.PI * 1900.0 / SR;
            p3 += 2 * Math.PI * 2850.0 / SR;
            buf[i] = (Math.Sin(p1) * 0.60 + Math.Sin(p2) * 0.28 + Math.Sin(p3) * 0.12)
                   * Math.Exp(-t * 5.5);
        }
        return BuildWav(Norm(buf, 26000));
    }

    /// <summary>별 — 상승 주파수 스파클</summary>
    private static byte[] GenerateStar()
    {
        const double dur = 0.22;
        var n = (int)(dur * SR);
        var buf = new double[n];
        var phase = 0.0;

        for (var i = 0; i < n; i++)
        {
            var tNorm = (double)i / n;
            phase    += 2 * Math.PI * (700 * Math.Pow(1600.0 / 700.0, tNorm)) / SR;
            buf[i]    = Math.Sin(phase) * Math.Sin(Math.PI * tNorm);
        }
        return BuildWav(Norm(buf, 24000));
    }

    /// <summary>놓침 — 하강 둔탁한 타격</summary>
    private static byte[] GenerateMiss()
    {
        const double dur = 0.38;
        var n = (int)(dur * SR);
        var buf = new double[n];
        var phase = 0.0;

        for (var i = 0; i < n; i++)
        {
            var t  = (double)i / SR;
            phase += 2 * Math.PI * (220 * Math.Exp(-t * 6)) / SR;
            var env = Math.Exp(-t * 6);
            buf[i]  = (Math.Sin(phase) * 0.8
                      + (Rng.NextDouble() * 2 - 1) * Math.Exp(-t * 12) * 0.35) * env;
        }
        return BuildWav(Norm(buf, 24000));
    }

    /// <summary>피버 — C5→E5→G5→C6 상승 아르페지오</summary>
    private static byte[] GenerateFever()
    {
        double[] notes = [523.25, 659.25, 783.99, 1046.50]; // C5, E5, G5, C6
        const double noteDur = 0.12, gap = 0.025;
        var n = (int)(notes.Length * (noteDur + gap) * SR);
        var buf = new double[n];

        for (var ni = 0; ni < notes.Length; ni++)
        {
            var off = (int)(ni * (noteDur + gap) * SR);
            var len = (int)(noteDur * SR);
            var ph  = 0.0;
            for (var i = 0; i < len && off + i < n; i++)
            {
                var tNorm = (double)i / len;
                ph    += 2 * Math.PI * notes[ni] / SR;
                var env = tNorm < 0.08 ? tNorm / 0.08 : Math.Exp(-(tNorm - 0.08) * 7);
                buf[off + i] = Math.Sin(ph) * env;
            }
        }
        return BuildWav(Norm(buf, 26000));
    }

    /// <summary>게임오버 — A4→F#4→D4→A3 하강 멜로디 (삼각파)</summary>
    private static byte[] GenerateGameOver()
    {
        double[] notes = [440.0, 369.99, 293.66, 220.0]; // A4, F#4, D4, A3
        const double noteDur = 0.42;
        var n = (int)((notes.Length * noteDur + 0.3) * SR);
        var buf = new double[n];

        for (var ni = 0; ni < notes.Length; ni++)
        {
            var off = (int)(ni * noteDur * SR);
            var len = (int)(noteDur * SR);
            var ph  = 0.0;
            for (var i = 0; i < len && off + i < n; i++)
            {
                var tNorm = (double)i / len;
                ph    += 2 * Math.PI * notes[ni] / SR;
                var tri = Math.Abs((ph % (2 * Math.PI)) / Math.PI - 1) * 2 - 1;
                var env = tNorm < 0.04 ? tNorm / 0.04 : Math.Exp(-(tNorm - 0.04) * 2.5);
                buf[off + i] = tri * env;
            }
        }
        return BuildWav(Norm(buf, 26000));
    }

    // ── BGM 생성 (Synthwave 4초 루프, 120 BPM) ───────────────────────────────

    private static byte[] GenerateBgm()
    {
        const double bpm  = 120.0;
        const double beat = 60.0 / bpm;       // 0.5s / beat
        var n = (int)(8 * beat * SR);          // 8 beats = 4 seconds
        var buf = new double[n];

        // Kick: 0, 2, 4, 6 박 + 5박 소프트 킥 (신코페이션)
        foreach (var b in new[] { 0, 2, 4, 6 }) AddKick(buf, (int)(b * beat * SR));
        AddKick(buf, (int)(5 * beat * SR), 0.6);

        // Snare: 1, 3, 5, 7 박 (백비트)
        foreach (var b in new[] { 1, 3, 5, 7 }) AddSnare(buf, (int)(b * beat * SR));

        // Hi-hat: 16분음표 (beat × 0.5 간격)
        for (var i = 0; i < 16; i++)
            AddHihat(buf, (int)(i * beat * 0.5 * SR), i % 2 == 0 ? 0.48 : 0.30);

        // Bass 사각파: A2-A2-E2-E2-A2-A2-D2-D2
        double[] bassF = [110.0, 110.0, 82.41, 82.41, 110.0, 110.0, 73.42, 73.42];
        for (var b = 0; b < 8; b++)
            AddBass(buf, (int)(b * beat * SR), bassF[b], (int)(beat * SR));

        // 리드 아르페지오 (8분음표): A 펜타토닉 멜로디
        double[] leadF =
        [
            440.0, 523.25, 659.25, 783.99, 880.0, 783.99, 659.25, 523.25,
            440.0, 523.25, 659.25, 783.99, 523.25, 659.25, 880.0, 659.25,
        ];
        for (var i = 0; i < 16; i++)
            AddLead(buf, (int)(i * beat * 0.5 * SR), leadF[i], (int)(beat * 0.45 * SR));

        // 패드 코드: A 단조 (A2, C3, E3) — 낮은 볼륨 배경
        AddPad(buf, 0, n, [110.0, 130.81, 164.81]);

        return BuildWav(Norm(buf, 24000));
    }

    private static void AddKick(double[] buf, int off, double amp = 1.0)
    {
        var len = Math.Min((int)(0.28 * SR), buf.Length - off);
        var ph  = 0.0;
        for (var i = 0; i < len; i++)
        {
            var t   = (double)i / SR;
            ph     += 2 * Math.PI * (80 * Math.Exp(-t * 28)) / SR;
            buf[off + i] += Math.Sin(ph) * Math.Exp(-t * 18) * amp
                          + (Rng.NextDouble() * 2 - 1) * Math.Exp(-t * 55) * 0.3 * amp;
        }
    }

    private static void AddSnare(double[] buf, int off)
    {
        var len = Math.Min((int)(0.22 * SR), buf.Length - off);
        for (var i = 0; i < len; i++)
        {
            var t   = (double)i / SR;
            var env = Math.Exp(-t * 28);
            buf[off + i] += ((Rng.NextDouble() * 2 - 1) * 0.85
                           + Math.Sin(2 * Math.PI * 180 * t) * 0.25) * env * 0.65;
        }
    }

    private static void AddHihat(double[] buf, int off, double amp)
    {
        var len = Math.Min((int)(0.055 * SR), buf.Length - off);
        for (var i = 0; i < len; i++)
        {
            var t = (double)i / SR;
            buf[off + i] += (Rng.NextDouble() * 2 - 1) * Math.Exp(-t * 90) * amp * 0.38;
        }
    }

    private static void AddBass(double[] buf, int off, double freq, int len)
    {
        len = Math.Min(len, buf.Length - off);
        var ph = 0.0;
        for (var i = 0; i < len; i++)
        {
            var tNorm = (double)i / len;
            ph += 2 * Math.PI * freq / SR;
            var saw = 2 * ((ph % (2 * Math.PI)) / (2 * Math.PI)) - 1;
            var env = tNorm < 0.02 ? tNorm / 0.02 : tNorm > 0.85 ? (1 - tNorm) / 0.15 : 1.0;
            buf[off + i] += saw * env * 0.32;
        }
    }

    private static void AddLead(double[] buf, int off, double freq, int len)
    {
        len = Math.Min(len, buf.Length - off);
        var ph = 0.0;
        for (var i = 0; i < len; i++)
        {
            var tNorm = (double)i / len;
            ph += 2 * Math.PI * freq / SR;
            var env = tNorm < 0.05 ? tNorm / 0.05 : tNorm > 0.75 ? (1 - tNorm) / 0.25 : 1.0;
            buf[off + i] += Math.Sin(ph) * env * 0.22;
        }
    }

    private static void AddPad(double[] buf, int start, int len, double[] freqs)
    {
        len = Math.Min(len, buf.Length - start);
        var phases = new double[freqs.Length];
        for (var i = 0; i < len; i++)
        {
            var tNorm = (double)i / len;
            var fenv  = tNorm < 0.08 ? tNorm / 0.08 : tNorm > 0.92 ? (1 - tNorm) / 0.08 : 1.0;
            double s  = 0;
            for (var f = 0; f < freqs.Length; f++)
            {
                phases[f] += 2 * Math.PI * freqs[f] / SR;
                s += Math.Sin(phases[f]);
            }
            buf[start + i] += s / freqs.Length * fenv * 0.12;
        }
    }
}
