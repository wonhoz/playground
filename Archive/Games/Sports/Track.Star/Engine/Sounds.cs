using static TrackStar.Engine.SoundGen;

namespace TrackStar.Engine;

/// <summary>
/// 올림픽 스포츠 BGM 및 SFX 정의.
/// </summary>
public static class Sounds
{
    // ── BGM ───────────────────────────────────────────
    public static readonly byte[] Bgm = Repeat(Mix(
        Seq([(G4, 0.15), (B4, 0.15), (D5, 0.15), (G5, 0.15), (D5, 0.15), (B4, 0.15), (G4, 0.15), (REST, 0.15)], wave: 1, vol: 0.2),
        Seq([(G3, 0.3), (D3, 0.3), (G3, 0.3), (D3, 0.3)], wave: 1, vol: 0.13)
    ), 8);

    // ── SFX ──────────────────────────────────────────
    public static readonly byte[] CountdownSfx = Tone(440, 0.15, vol: 0.4, wave: 0);
    public static readonly byte[] GoSfx = Tone(880, 0.3, vol: 0.4, wave: 0);
    public static readonly byte[] StepSfx = Noise(0.04, 0.15);
    public static readonly byte[] JumpSfx = Sweep(200, 600, 0.15);
    public static readonly byte[] FinishSfx = Seq([(C5, 0.12), (E5, 0.12), (G5, 0.12), (1047, 0.12)]);
    public static readonly byte[] MedalSfx = Sweep(400, 1200, 0.4);
}
