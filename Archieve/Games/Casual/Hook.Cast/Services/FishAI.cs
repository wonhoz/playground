namespace HookCast.Services;

public class FishAI
{
    private static readonly Random Rng = new();

    // ── 물고기 풀 생성 ─────────────────────────────────────────────
    public static List<Fish> SpawnFish(Weather weather, double canvasW, double waterY, double canvasH)
    {
        int count = weather switch
        {
            Weather.Sunny  => Rng.Next(4, 7),
            Weather.Cloudy => Rng.Next(5, 9),
            Weather.Rainy  => Rng.Next(2, 5), // 비 오면 활성도 감소
            _ => 5
        };

        var fish = new List<Fish>();
        for (int i = 0; i < count; i++)
            fish.Add(SpawnOneFish(canvasW, waterY, canvasH));
        return fish;
    }

    private static Fish SpawnOneFish(double w, double waterY, double h)
    {
        var species = (FishSpecies)Rng.Next(0, 5);
        var (spd, look, fight, minSize, maxSize) = species switch
        {
            FishSpecies.Crucian   => (1.2, 80.0,  0.3, 15.0, 35.0),
            FishSpecies.Bass      => (2.0, 100.0, 0.5, 20.0, 50.0),
            FishSpecies.Trout     => (3.0, 110.0, 0.6, 25.0, 55.0),
            FishSpecies.Salmon    => (2.5, 120.0, 0.8, 40.0, 80.0),
            FishSpecies.Snakehead => (2.2, 90.0,  1.0, 30.0, 70.0),
            _ => (2.0, 100.0, 0.5, 20.0, 50.0)
        };
        double size   = minSize + Rng.NextDouble() * (maxSize - minSize);
        double weight = size * size * 0.0003 * (0.8 + Rng.NextDouble() * 0.4);

        return new Fish
        {
            Species     = species,
            Pos         = new Vec2(Rng.NextDouble() * w, waterY + 30 + Rng.NextDouble() * (h - waterY - 60)),
            Velocity    = new Vec2((Rng.NextDouble() - 0.5) * spd, 0),
            Size        = size,
            Weight      = weight,
            Speed       = spd,
            LookRange   = look,
            FightStrength = fight,
        };
    }

    // ── 프레임별 AI 업데이트 ──────────────────────────────────────
    public static void UpdateFish(Fish fish, Vec2 hookPos, bool hookInWater,
                                  double canvasW, double waterY, double canvasH,
                                  out bool biteTriggered)
    {
        biteTriggered = false;

        if (fish.BiteCooldown > 0) fish.BiteCooldown--;

        switch (fish.State)
        {
            case FishState.Roaming:
                Roam(fish, canvasW, waterY, canvasH);
                if (hookInWater && fish.BiteCooldown <= 0)
                {
                    var dist = (hookPos - fish.Pos).Length;
                    if (dist < fish.LookRange)
                        fish.State = FishState.Approaching;
                }
                break;

            case FishState.Approaching:
                if (!hookInWater) { fish.State = FishState.Roaming; break; }
                var toHook = (hookPos - fish.Pos).Normalized * fish.Speed * 1.5;
                fish.Pos = fish.Pos + toHook;
                if ((hookPos - fish.Pos).Length < 14)
                {
                    fish.State    = FishState.Biting;
                    biteTriggered = true;
                }
                break;

            case FishState.Biting:
                // 입질 상태 유지 (UI에서 챔질 감지)
                break;

            case FishState.Escaped:
                // 탈출 후 잠시 도주
                Roam(fish, canvasW, waterY, canvasH, fastEscape: true);
                fish.State       = FishState.Roaming;
                fish.BiteCooldown = 180;
                break;
        }
    }

    private static void Roam(Fish fish, double w, double waterY, double h, bool fastEscape = false)
    {
        var spd = fastEscape ? fish.Speed * 3 : fish.Speed;
        var newPos = fish.Pos + fish.Velocity * spd;

        // 경계 반사
        if (newPos.X < 20 || newPos.X > w - 20)
            fish.Velocity = new Vec2(-fish.Velocity.X, fish.Velocity.Y);
        if (newPos.Y < waterY + 20 || newPos.Y > h - 20)
            fish.Velocity = new Vec2(fish.Velocity.X, -fish.Velocity.Y);

        // 가끔 방향 변경
        if (new Random().NextDouble() < 0.01)
            fish.Velocity = new Vec2((new Random().NextDouble() - 0.5) * 2, (new Random().NextDouble() - 0.5) * 0.3);

        fish.Pos = new Vec2(
            Math.Clamp(newPos.X, 20, w - 20),
            Math.Clamp(newPos.Y, waterY + 20, h - 20));
    }
}
