using System.IO;
using System.Runtime.InteropServices;

namespace HueFlow.Sound;

/// <summary>
/// 외부 라이브러리 없이 PCM 사인파 WAV를 메모리에서 생성 후
/// winmm.dll PlaySound(SND_MEMORY)로 비동기 재생.
/// </summary>
internal static class SoundHelper
{
    [DllImport("winmm.dll", SetLastError = false)]
    private static extern bool PlaySound(byte[] pszSound, IntPtr hmod, uint fdwSound);

    private const uint SND_ASYNC    = 0x0001;
    private const uint SND_MEMORY   = 0x0004;
    private const uint SND_NODEFAULT = 0x0002;

    // 미리 생성해 둔 WAV 바이트 — 앱 시작 시 한 번만 빌드
    private static readonly byte[] _clickWav = BuildWav(frequency: 880, durationMs: 65,  amplitude: 0.55);
    private static readonly byte[] _winWav   = BuildWav(frequency: 1320, durationMs: 110, amplitude: 0.60);
    private static readonly byte[] _loseWav  = BuildWav(frequency: 330,  durationMs: 180, amplitude: 0.50);

    public static void PlayClick() => Play(_clickWav);
    public static void PlayWin()   => Play(_winWav);
    public static void PlayLose()  => Play(_loseWav);

    private static void Play(byte[] wav) =>
        PlaySound(wav, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);

    // ── PCM WAV 생성 (16-bit, 44100Hz, Mono, 지수 감쇠 엔벨로프) ─────
    private static byte[] BuildWav(double frequency, double durationMs, double amplitude)
    {
        const int sampleRate = 44100;
        int numSamples = (int)(sampleRate * durationMs / 1000.0);
        var samples    = new short[numSamples];

        // 지수 감쇠 엔벨로프: 빠른 어택 + 자연스러운 퇴조
        double decayK = 8.0 / (durationMs / 1000.0);  // duration 안에 충분히 감쇠

        for (int i = 0; i < numSamples; i++)
        {
            double t        = (double)i / sampleRate;
            double envelope = Math.Exp(-decayK * t);
            double wave     = Math.Sin(2 * Math.PI * frequency * t);
            samples[i] = (short)(wave * envelope * amplitude * short.MaxValue);
        }

        // WAV 파일 포맷 (RIFF/PCM)
        const int channels  = 1;
        const int bitDepth  = 16;
        int blockAlign      = channels * (bitDepth / 8);
        int byteRate        = sampleRate * blockAlign;
        int dataSize        = numSamples * blockAlign;

        using var ms = new MemoryStream(44 + dataSize);
        using var w  = new BinaryWriter(ms);

        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataSize);
        w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16);
        w.Write((short)1);              // PCM
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write((short)blockAlign);
        w.Write((short)bitDepth);
        w.Write("data"u8.ToArray()); w.Write(dataSize);
        foreach (var s in samples) w.Write(s);

        return ms.ToArray();
    }
}
