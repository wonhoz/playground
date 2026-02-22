namespace DodgeBlitz.Engine;

/// <summary>
/// BGM 및 SFX 사운드 데이터.
/// </summary>
public static class Sounds
{
    public static readonly byte[] HitSfx      = SoundGen.Noise(0.3, 0.55, 2.0);
    public static readonly byte[] LevelUpSfx  = SoundGen.Sweep(400, 900, 0.18, 0.35);
    public static readonly byte[] GameOverSfx = SoundGen.Seq(
    [
        (SoundGen.A4, 0.15), (SoundGen.G4, 0.15), (SoundGen.F4, 0.15),
        (SoundGen.E4, 0.15), (SoundGen.D4, 0.2),  (SoundGen.C4, 0.35)
    ], wave: 0, vol: 0.3);

    public static readonly byte[] Bgm;

    static Sounds()
    {
        // 긴장감 있는 BGM - 짧고 반복적인 멜로디
        var melody = SoundGen.Seq(
        [
            (SoundGen.E5, 0.1), (SoundGen.REST, 0.05), (SoundGen.E5, 0.1),
            (SoundGen.D5, 0.1), (SoundGen.C5, 0.2),
            (SoundGen.D5, 0.1), (SoundGen.REST, 0.05), (SoundGen.D5, 0.1),
            (SoundGen.C5, 0.1), (SoundGen.B4, 0.2),
            (SoundGen.C5, 0.1), (SoundGen.E5, 0.1), (SoundGen.G5, 0.15),
            (SoundGen.A5, 0.1), (SoundGen.REST, 0.15)
        ], wave: 1, vol: 0.18);

        var bass = SoundGen.Seq(
        [
            (SoundGen.C3, 0.2), (SoundGen.C3, 0.2),
            (SoundGen.G3, 0.2), (SoundGen.G3, 0.2),
            (SoundGen.A3, 0.2), (SoundGen.A3, 0.2),
            (SoundGen.F3, 0.2), (SoundGen.F3, 0.2),
            (SoundGen.C3, 0.2), (SoundGen.C3, 0.15)
        ], wave: 2, vol: 0.11);

        Bgm = SoundGen.Repeat(SoundGen.Mix(melody, bass), 10);
    }
}
