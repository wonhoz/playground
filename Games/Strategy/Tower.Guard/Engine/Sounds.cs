using static TowerGuard.Engine.SoundGen;

namespace TowerGuard.Engine;

public static class Sounds
{
    // ── SFX ──────────────────────────────────────────
    public static readonly byte[] TowerPlaceSfx = Tone(550, 0.1, 0.3, 0);
    public static readonly byte[] TowerShootSfx = Tone(880, 0.04, 0.25, 1);
    public static readonly byte[] EnemyDeathSfx = Noise(0.1, 0.25);
    public static readonly byte[] WaveStartSfx = Sweep(300, 700, 0.3);
    public static readonly byte[] VictorySfx = Seq([
        (Bb4, 0.15), (D5, 0.15), (F5, 0.15), (932.0, 0.15)
    ], wave: 1, vol: 0.3);
    public static readonly byte[] GameOverSfx = Seq([
        (F4, 0.2), (Eb4, 0.2), (Bb3, 0.2)
    ], wave: 1, vol: 0.3);

    // ── BGM ──────────────────────────────────────────
    public static readonly byte[] Bgm = BuildBgm();

    private static byte[] BuildBgm()
    {
        var melody = Seq([
            (Bb4, 0.2), (Eb4, 0.2), (F4, 0.2), (Bb4, 0.2),
            (F4, 0.2), (Eb4, 0.2), (Bb3, 0.2), (REST, 0.2)
        ], wave: 3, vol: 0.18);

        var bass = Seq([
            (Bb3, 0.4), (F3, 0.4), (Bb3, 0.4), (F3, 0.4)
        ], wave: 0, vol: 0.12);

        var loop = Mix(melody, bass);
        return Repeat(loop, 8);
    }
}
