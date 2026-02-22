using static BrickBlitz.Engine.SoundGen;

namespace BrickBlitz.Engine;

public static class Sounds
{
    // ── SFX ──────────────────────────────────────────
    public static readonly byte[] BallHitSfx = Tone(660, 0.04, 0.3, 1);
    public static readonly byte[] BrickBreakSfx = Noise(0.06, 0.35);
    public static readonly byte[] PowerUpSfx = Sweep(400, 1000, 0.15);
    public static readonly byte[] LifeLostSfx = Sweep(600, 200, 0.3);
    public static readonly byte[] StageClearSfx = Seq([
        (C5, 0.12), (E5, 0.12), (G5, 0.12), (1047.0, 0.12)
    ], wave: 1, vol: 0.3);

    // ── BGM ──────────────────────────────────────────
    public static readonly byte[] Bgm = BuildBgm();

    private static byte[] BuildBgm()
    {
        var melody = Seq([
            (C5, 0.12), (E5, 0.12), (G5, 0.12), (1047.0, 0.12),
            (G5, 0.12), (E5, 0.12), (C5, 0.12), (REST, 0.12)
        ], wave: 1, vol: 0.18);

        var bass = Seq([
            (C3, 0.24), (G3, 0.24), (C3, 0.24), (G3, 0.24)
        ], wave: 1, vol: 0.12);

        var loop = Mix(melody, bass);
        return Repeat(loop, 10);
    }
}
