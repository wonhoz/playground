using static DungeonDash.Engine.SoundGen;

namespace DungeonDash.Engine;

/// <summary>
/// 던전 크롤러 BGM 및 SFX 정의.
/// </summary>
public static class Sounds
{
    // ── BGM ───────────────────────────────────────────
    public static readonly byte[] Bgm = Repeat(Mix(
        Seq([(D4, 0.25), (F4, 0.25), (A4, 0.25), (D5, 0.25), (Bb4, 0.25), (A4, 0.25), (F4, 0.25), (D4, 0.25)], wave: 3, vol: 0.18),
        Seq([(D3, 0.5), (D3, 0.5), (A3, 0.5), (Bb3, 0.5)], wave: 0, vol: 0.12)
    ), 6);

    // ── SFX ──────────────────────────────────────────
    public static readonly byte[] AttackSfx = Sweep(300, 150, 0.08);
    public static readonly byte[] MonsterHitSfx = Noise(0.1, 0.3);
    public static readonly byte[] ItemPickupSfx = Sweep(500, 1200, 0.15);
    public static readonly byte[] StairsSfx = Seq([(C4, 0.1), (E4, 0.1), (G4, 0.1), (C5, 0.1)]);
    public static readonly byte[] DeathSfx = Sweep(500, 100, 0.5);
    public static readonly byte[] DoorSfx = Tone(440, 0.1);
}
