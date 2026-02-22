namespace DashCity.Engine;

public static class Sounds
{
    public static readonly byte[] Bgm;
    public static readonly byte[] CoinSfx = SoundGen.Tone(880, 0.05, 0.3);
    public static readonly byte[] JumpSfx = SoundGen.Sweep(200, 500, 0.1, 0.3);
    public static readonly byte[] SlideSfx = SoundGen.Noise(0.1, 0.2, 2.0);
    public static readonly byte[] CrashSfx = SoundGen.Noise(0.4, 0.45, 1.0);
    public static readonly byte[] PowerUpSfx = SoundGen.Sweep(400, 1200, 0.2, 0.35);
    public static readonly byte[] ShieldHitSfx = SoundGen.Noise(0.15, 0.3, 1.5);
    public static readonly byte[] GameOverSfx = SoundGen.Seq([(SoundGen.D4, 0.2), (SoundGen.Bb3, 0.2), (SoundGen.A3, 0.3)], 0, 0.3);

    static Sounds()
    {
        var melody = SoundGen.Seq([
            (SoundGen.D5, 0.1), (SoundGen.Gb5, 0.1), (SoundGen.A5, 0.1), (SoundGen.D5, 0.1),
            (SoundGen.A5, 0.1), (SoundGen.Gb5, 0.1), (SoundGen.D5, 0.1), (SoundGen.REST, 0.1)
        ], 1, 0.2);
        var bass = SoundGen.Seq([
            (SoundGen.D3, 0.2), (SoundGen.A3, 0.2), (SoundGen.D3, 0.2), (SoundGen.A3, 0.2)
        ], 2, 0.13);
        Bgm = SoundGen.Repeat(SoundGen.Mix(melody, bass), 12);
    }
}
