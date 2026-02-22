using static NitroDrift.Engine.SoundGen;

namespace NitroDrift.Engine;

/// <summary>
/// 탑다운 레이싱 BGM 및 SFX 정의.
/// </summary>
public static class Sounds
{
    // ── BGM ───────────────────────────────────────────
    public static readonly byte[] Bgm = Repeat(Mix(
        Seq([(F4, 0.1), (A4, 0.1), (C5, 0.1), (F5, 0.1), (C5, 0.1), (A4, 0.1), (F4, 0.1), (REST, 0.1)], wave: 2, vol: 0.18),
        Seq([(F3, 0.2), (F3, 0.2), (C3, 0.2), (C3, 0.2)], wave: 1, vol: 0.12)
    ), 10);

    // ── SFX ──────────────────────────────────────────
    public static readonly byte[] BoostSfx = Sweep(200, 800, 0.2);
    public static readonly byte[] LapSfx = Tone(660, 0.15, vol: 0.4);
    public static readonly byte[] CountdownSfx = Tone(440, 0.2, vol: 0.4, wave: 0);
    public static readonly byte[] GoSfx = Tone(880, 0.3, vol: 0.4, wave: 0);
    public static readonly byte[] FinishSfx = Seq([(C5, 0.15), (E5, 0.15), (G5, 0.15)]);
    public static readonly byte[] CrashSfx = Noise(0.2, 0.3);
}
