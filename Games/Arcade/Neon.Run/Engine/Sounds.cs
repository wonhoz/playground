namespace NeonRun.Engine;

public static class Sounds
{
    public static readonly byte[] Bgm;
    public static readonly byte[] CrystalSfx = SoundGen.Tone(1047, 0.06, 0.35);
    public static readonly byte[] CrashSfx = SoundGen.Noise(0.4, 0.5, 1.0);
    public static readonly byte[] JumpSfx = SoundGen.Sweep(200, 600, 0.12, 0.3);
    public static readonly byte[] GameOverSfx = SoundGen.Seq([(SoundGen.E4, 0.2), (SoundGen.D4, 0.2), (SoundGen.C4, 0.2), (SoundGen.B3, 0.3)], 0, 0.3);

    static Sounds()
    {
        var melody = SoundGen.Seq([
            (SoundGen.A4, 0.12), (SoundGen.C5, 0.12), (SoundGen.E5, 0.12), (SoundGen.A5, 0.12),
            (SoundGen.G5, 0.12), (SoundGen.E5, 0.12), (SoundGen.C5, 0.12), (SoundGen.A4, 0.12)
        ], 2, 0.18);
        var bass = SoundGen.Seq([
            (SoundGen.A3, 0.24), (SoundGen.A3, 0.24), (SoundGen.E3, 0.24), (SoundGen.E3, 0.24)
        ], 1, 0.13);
        Bgm = SoundGen.Repeat(SoundGen.Mix(melody, bass), 10);
    }
}
