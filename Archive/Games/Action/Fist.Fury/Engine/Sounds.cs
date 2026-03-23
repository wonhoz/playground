namespace FistFury.Engine;

/// <summary>
/// BGM 및 SFX 사운드 데이터.
/// </summary>
public static class Sounds
{
    public static readonly byte[] Bgm;
    public static readonly byte[] PunchSfx = SoundGen.Noise(0.08, 0.4, 2.0);
    public static readonly byte[] KickSfx = SoundGen.Noise(0.12, 0.45, 1.5);
    public static readonly byte[] HitSfx = SoundGen.Sweep(200, 100, 0.1, 0.35);
    public static readonly byte[] KOSfx = SoundGen.Noise(0.5, 0.5, 1.0);
    public static readonly byte[] WaveClearSfx = SoundGen.Sweep(300, 900, 0.3, 0.4);
    public static readonly byte[] GameOverSfx = SoundGen.Seq(
    [
        (SoundGen.E4, 0.2), (SoundGen.D4, 0.2), (SoundGen.C4, 0.2), (SoundGen.B3, 0.2)
    ], wave: 0, vol: 0.25);

    static Sounds()
    {
        var melody = SoundGen.Seq(
        [
            (SoundGen.E4, 0.12), (SoundGen.G4, 0.12), (SoundGen.B4, 0.12), (SoundGen.E5, 0.12),
            (SoundGen.D5, 0.12), (SoundGen.B4, 0.12), (SoundGen.G4, 0.12), (SoundGen.E4, 0.12)
        ], wave: 1, vol: 0.2);

        var bass = SoundGen.Seq(
        [
            (SoundGen.E3, 0.24), (SoundGen.E3, 0.24), (SoundGen.B3, 0.24), (SoundGen.B3, 0.24)
        ], wave: 2, vol: 0.15);

        Bgm = SoundGen.Repeat(SoundGen.Mix(melody, bass), 8);
    }
}
