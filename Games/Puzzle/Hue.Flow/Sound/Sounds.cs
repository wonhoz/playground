namespace HueFlow.Sound;

/// <summary>
/// BGM 및 SFX 사운드 데이터.
/// </summary>
public static class Sounds
{
    public static readonly byte[] Bgm;
    public static readonly byte[] ClickSfx = SoundGen.Tone(880, 0.065, 0.55);
    public static readonly byte[] WinSfx = SoundGen.Sweep(400, 1200, 0.3, 0.4);
    public static readonly byte[] LoseSfx = SoundGen.Sweep(600, 200, 0.4, 0.35);
    public static readonly byte[] ComboSfx = SoundGen.Tone(1200, 0.08, 0.3);

    static Sounds()
    {
        var melody = SoundGen.Seq(
        [
            (SoundGen.C4, 0.3), (SoundGen.E4, 0.3), (SoundGen.G4, 0.3),
            (SoundGen.C5, 0.3), (SoundGen.G4, 0.3), (SoundGen.E4, 0.3)
        ], wave: 0, vol: 0.15);

        var bass = SoundGen.Seq(
        [
            (SoundGen.C3, 0.9), (SoundGen.G3, 0.9)
        ], wave: 0, vol: 0.1);

        Bgm = SoundGen.Repeat(SoundGen.Mix(melody, bass), 6);
    }
}
