namespace StarStrike.Engine;

/// <summary>
/// BGM 및 SFX 사운드 데이터.
/// </summary>
public static class Sounds
{
    public static readonly byte[] Bgm;
    public static readonly byte[] ShootSfx = SoundGen.Tone(880, 0.06, 0.3, wave: 1);
    public static readonly byte[] ExplosionSfx = SoundGen.Noise(0.3, 0.4);
    public static readonly byte[] PowerUpSfx = SoundGen.Sweep(300, 1000, 0.2, 0.4);
    public static readonly byte[] HitSfx = SoundGen.Noise(0.1, 0.3);
    public static readonly byte[] GameOverSfx = SoundGen.Seq(
    [
        (SoundGen.E4, 0.2), (SoundGen.D4, 0.2), (SoundGen.C4, 0.2), (SoundGen.B3, 0.2)
    ], wave: 0, vol: 0.25);

    static Sounds()
    {
        var melody = SoundGen.Seq(
        [
            (SoundGen.A4, 0.15), (SoundGen.C5, 0.15), (SoundGen.E5, 0.15), (SoundGen.A5, 0.15),
            (SoundGen.E5, 0.15), (SoundGen.C5, 0.15), (SoundGen.A4, 0.15), (SoundGen.REST, 0.15)
        ], wave: 1, vol: 0.2);

        var bass = SoundGen.Seq(
        [
            (SoundGen.A3, 0.3), (SoundGen.A3, 0.3), (SoundGen.E3, 0.3), (SoundGen.E3, 0.3)
        ], wave: 1, vol: 0.15);

        Bgm = SoundGen.Repeat(SoundGen.Mix(melody, bass), 8);
    }
}
