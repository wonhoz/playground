using static BeatDrop.Engine.SoundGen;

namespace BeatDrop.Engine;

public static class Sounds
{
    // ── SFX ──────────────────────────────────────────
    public static readonly byte[] PerfectSfx = Tone(880, 0.05, 0.4, 0);
    public static readonly byte[] GreatSfx = Tone(660, 0.05, 0.35, 0);
    public static readonly byte[] GoodSfx = Tone(440, 0.05, 0.3, 0);
    public static readonly byte[] MissSfx = Noise(0.08, 0.2);
    public static readonly byte[] ComboSfx = Sweep(600, 1200, 0.1);

    // ── BGM ──────────────────────────────────────────
    public static readonly byte[] Bgm = BuildBgm();

    private static byte[] BuildBgm()
    {
        var melody = Seq([
            (E4, 0.15), (E4, 0.15), (G4, 0.15), (A4, 0.15),
            (G4, 0.15), (E4, 0.15), (D4, 0.15), (E4, 0.15)
        ], wave: 1, vol: 0.2);

        var bass = Seq([
            (E3, 0.3), (REST, 0.3), (E3, 0.3), (REST, 0.3)
        ], wave: 1, vol: 0.15);

        var kick = Noise(0.05, 0.2, 2.0);
        var hihat = Noise(0.02, 0.1, 3.0);
        var silence = Tone(REST, 0.13, 0);
        var drums = Mix(
            Seq([(REST, 0.0)], vol: 0),
            kick,
            Seq([(REST, 0.15)], vol: 0),
            hihat,
            Seq([(REST, 0.30)], vol: 0),
            kick,
            Seq([(REST, 0.45)], vol: 0),
            hihat,
            Seq([(REST, 0.60)], vol: 0),
            kick,
            Seq([(REST, 0.75)], vol: 0),
            hihat,
            Seq([(REST, 0.90)], vol: 0),
            kick,
            Seq([(REST, 1.05)], vol: 0),
            hihat
        );

        var loop = Mix(melody, bass, drums);
        return Repeat(loop, 8);
    }
}
