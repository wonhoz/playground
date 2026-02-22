using static GravityFlip.Engine.SoundGen;

namespace GravityFlip.Engine;

public static class Sounds
{
    public static readonly byte[] Bgm = Repeat(Mix(
        Seq([(E4, 0.3), (G4, 0.3), (B4, 0.3), (E5, 0.3), (D5, 0.3), (B4, 0.3), (A4, 0.3), (G4, 0.3)], wave: 0, vol: 0.15),
        Seq([(E3, 0.6), (B3, 0.6), (A3, 0.6), (G3, 0.6)], wave: 0, vol: 0.1)
    ), 5);

    public static readonly byte[] FlipSfx = Sweep(300, 600, 0.1);
    public static readonly byte[] CoinSfx = Tone(880, 0.08, 0.35, 0);
    public static readonly byte[] DeathSfx = Noise(0.3, 0.4, 1.0);
    public static readonly byte[] PortalSfx = Sweep(400, 1200, 0.4);
    public static readonly byte[] LevelStartSfx = Seq([(E4, 0.1), (G4, 0.1), (B4, 0.1), (E5, 0.1)], wave: 0, vol: 0.25);
}
