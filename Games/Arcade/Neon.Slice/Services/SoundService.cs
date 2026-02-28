using System.IO;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NeonSlice.Services;

public enum SoundCue { Slice, SliceFever, Bomb, Lightning, Ice, Star, Miss, Fever, GameOver }

/// <summary>
/// PCM WAV 코드 합성 기반 사운드 서비스.
/// System.Windows.Media.MediaPlayer를 사용해 BGM과 SFX가 동시에 재생됩니다.
/// (System.Media.SoundPlayer는 PlaySound API 단일 채널 한계로 BGM 중단 문제 발생)
/// </summary>
public sealed class SoundService : IDisposable
{
    private const int SR = 44100;
    private static readonly Random Rng = new();

    // 임시 WAV 파일 목록 (Dispose 시 삭제)
    private readonly List<string> _tempFiles = [];

    // BGM: MediaTimeline + RepeatBehavior.Forever → 끊김 없는 루프
    private readonly MediaPlayer _bgm = new();
    private MediaClock? _bgmClock;
    private string _bgmPath = "";

    // SFX 풀 (동시 슬라이스 대응)
    private readonly MediaPlayer[] _slicePool = [new(), new(), new()];
    private readonly MediaPlayer[] _feverPool = [new(), new(), new()];
    private int _sliceIdx;
    private int _feverIdx;

    private readonly MediaPlayer _bomb;
    private readonly MediaPlayer _lightning;
    private readonly MediaPlayer _ice;
    private readonly MediaPlayer _star;
    private readonly MediaPlayer _miss;
    private readonly MediaPlayer _fever;
    private readonly MediaPlayer _gameOver;

    public SoundService()
    {
        _bomb      = new(); _lightning = new(); _ice     = new();
        _star      = new(); _miss      = new(); _fever   = new();
        _gameOver  = new();

        // 임시 파일에 WAV 쓰기 후 MediaPlayer에 로드
        _bgmPath = TempWav(GenerateBgm());

        foreach (var p in _slicePool) p.Open(new Uri(TempWav(GenerateSlice(false))));
        foreach (var p in _feverPool) p.Open(new Uri(TempWav(GenerateSlice(true))));
        _bomb.Open     (new Uri(TempWav(GenerateBomb())));
        _lightning.Open(new Uri(TempWav(GenerateLightning())));
        _ice.Open      (new Uri(TempWav(GenerateIce())));
        _star.Open     (new Uri(TempWav(GenerateStar())));
        _miss.Open     (new Uri(TempWav(GenerateMiss())));
        _fever.Open    (new Uri(TempWav(GenerateFever())));
        _gameOver.Open (new Uri(TempWav(GenerateGameOver())));
    }

    // WAV 바이트를 임시 파일로 저장하고 경로 반환
    private string TempWav(byte[] wav)
    {
        var path = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
        File.WriteAllBytes(path, wav);
        _tempFiles.Add(path);
        return path;
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────

    public void StartBgm()
    {
        if (_bgmClock != null) return; // 이미 재생 중
        var tl = new MediaTimeline(new Uri(_bgmPath)) { RepeatBehavior = RepeatBehavior.Forever };
        _bgmClock = tl.CreateClock();
        _bgm.Clock = _bgmClock;
        _bgmClock.Controller.Begin();
    }

    public void StopBgm()
    {
        if (_bgmClock == null) return;
        _bgmClock.Controller.Stop();
        _bgm.Clock = null;
        _bgmClock = null;
    }

    public void Play(SoundCue cue)
    {
        switch (cue)
        {
            case SoundCue.Slice:      PlayPool(_slicePool, ref _sliceIdx); break;
            case SoundCue.SliceFever: PlayPool(_feverPool, ref _feverIdx); break;
            case SoundCue.Bomb:       PlaySfx(_bomb);      break;
            case SoundCue.Lightning:  PlaySfx(_lightning); break;
            case SoundCue.Ice:        PlaySfx(_ice);       break;
            case SoundCue.Star:       PlaySfx(_star);      break;
            case SoundCue.Miss:       PlaySfx(_miss);      break;
            case SoundCue.Fever:      PlaySfx(_fever);     break;
            case SoundCue.GameOver:   StopBgm(); PlaySfx(_gameOver); break;
        }
    }

    public void Dispose()
    {
        StopBgm();
        _bgm.Close();
        foreach (var p in _slicePool) { p.Stop(); p.Close(); }
        foreach (var p in _feverPool) { p.Stop(); p.Close(); }
        _bomb.Close(); _lightning.Close(); _ice.Close();
        _star.Close(); _miss.Close(); _fever.Close(); _gameOver.Close();
        foreach (var f in _tempFiles) try { File.Delete(f); } catch { }
    }

    private static void PlayPool(MediaPlayer[] pool, ref int idx)
    {
        var p = pool[idx++ % pool.Length];
        p.Stop();
        p.Position = TimeSpan.Zero;
        p.Play();
    }

    private static void PlaySfx(MediaPlayer p)
    {
        p.Stop();
        p.Position = TimeSpan.Zero;
        p.Play();
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
            buf[i] = (Math.Sin(phase) * 0.80
                    + (Rng.NextDouble() * 2 - 1) * Math.Exp(-t * 12) * 0.35)
                   * Math.Exp(-t * 6);
        }
        return BuildWav(Norm(buf, 24000));
    }

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

    // ── BGM (Synthwave 루프, 120 BPM) ────────────────────────────────────────
    // MediaTimeline + RepeatBehavior.Forever로 끊김 없이 루프하므로
    // 패드에 페이드 인/아웃 없이 일정 볼륨 유지 (loop point 자연스러움)
    private static byte[] GenerateBgm()
    {
        const double bpm  = 120.0;
        const double beat = 60.0 / bpm;      // 0.5s
        var n = (int)(8 * beat * SR);         // 8 beats = 4s

        var buf = new double[n];

        // 킥: 4-on-the-floor (0, 2, 4, 6박)
        foreach (var b in new[] { 0, 2, 4, 6 }) AddKick(buf, (int)(b * beat * SR));

        // 스네어: 백비트 (1, 3, 5, 7박)
        foreach (var b in new[] { 1, 3, 5, 7 }) AddSnare(buf, (int)(b * beat * SR));

        // 하이햇: 8분음표 (0.5박 간격), 볼륨 낮춤
        for (var i = 0; i < 16; i++)
            AddHihat(buf, (int)(i * beat * 0.5 * SR), i % 2 == 0 ? 0.28 : 0.16);

        // 베이스 사각파: A2-A2-A2-A2-E2-E2-E2-E2
        double[] bassF = [110.0, 110.0, 110.0, 110.0, 82.41, 82.41, 82.41, 82.41];
        for (var b = 0; b < 8; b++)
            AddBass(buf, (int)(b * beat * SR), bassF[b], (int)(beat * SR));

        // 리드 멜로디: 4분음표 (1박 간격) — 8분음표 아르페지오보다 여유 있음
        double[] leadF = [440.0, 523.25, 659.25, 523.25, 659.25, 783.99, 659.25, 523.25];
        for (var i = 0; i < 8; i++)
            AddLead(buf, (int)(i * beat * SR), leadF[i], (int)(beat * 0.80 * SR));

        // 패드 코드: A 단조 (A2, C3, E3) — 페이드 없이 일정 볼륨
        AddPad(buf, 0, n, [110.0, 130.81, 164.81]);

        return BuildWav(Norm(buf, 22000));
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
            var env = tNorm < 0.02 ? tNorm / 0.02 : tNorm > 0.88 ? (1 - tNorm) / 0.12 : 1.0;
            buf[off + i] += saw * env * 0.30;
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
            buf[off + i] += Math.Sin(ph) * env * 0.20;
        }
    }

    // 패드: 페이드 없이 일정 볼륨 (MediaTimeline 루프 시 loop point 자연스러움)
    private static void AddPad(double[] buf, int start, int len, double[] freqs)
    {
        len = Math.Min(len, buf.Length - start);
        var phases = new double[freqs.Length];
        for (var i = 0; i < len; i++)
        {
            double s = 0;
            for (var f = 0; f < freqs.Length; f++)
            {
                phases[f] += 2 * Math.PI * freqs[f] / SR;
                s += Math.Sin(phases[f]);
            }
            buf[start + i] += s / freqs.Length * 0.12;
        }
    }
}
