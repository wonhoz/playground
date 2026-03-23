namespace WiFiCast.Services;

/// <summary>채널 간섭 점수를 계산하고 최적 채널을 추천합니다.</summary>
public static class ChannelAnalyzer
{
    // 2.4GHz 채널 목록 (한국: 1~13)
    private static readonly int[] Channels24 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];

    // 5GHz 주요 채널 (UNII-1/2/2e/3)
    private static readonly int[] Channels5 =
    [
        36, 40, 44, 48,           // UNII-1
        52, 56, 60, 64,           // UNII-2
        100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, // UNII-2e
        149, 153, 157, 161, 165,  // UNII-3
    ];

    // ── 공개 API ──────────────────────────────────────────────────────

    /// <summary>2.4GHz 기준 최적 채널 (1, 6, 11 중) 반환.</summary>
    public static int BestChannel24(IReadOnlyList<WifiNetwork> networks)
    {
        var candidates = new[] { 1, 6, 11 };
        return candidates.MinBy(ch => InterferenceScore24(networks, ch));
    }

    /// <summary>5GHz 기준 최적 채널 반환.</summary>
    public static int BestChannel5(IReadOnlyList<WifiNetwork> networks)
    {
        var nets5 = networks.Where(n => n.Band == "5GHz").ToList();
        if (nets5.Count == 0) return Channels5[0];
        return Channels5.MinBy(ch => InterferenceScore5(nets5, ch));
    }

    /// <summary>채널별 간섭 점수 맵 (2.4GHz).</summary>
    public static Dictionary<int, double> ScoreMap24(IReadOnlyList<WifiNetwork> networks) =>
        Channels24.ToDictionary(ch => ch, ch => InterferenceScore24(networks, ch));

    /// <summary>채널별 간섭 점수 맵 (5GHz).</summary>
    public static Dictionary<int, double> ScoreMap5(IReadOnlyList<WifiNetwork> networks)
    {
        var nets5 = networks.Where(n => n.Band == "5GHz").ToList();
        return Channels5.ToDictionary(ch => ch, ch => InterferenceScore5(nets5, ch));
    }

    // ── 내부 계산 ─────────────────────────────────────────────────────

    // 2.4GHz: 22MHz 대역폭 → 채널 간격 ≤4이면 겹침
    private static double InterferenceScore24(IReadOnlyList<WifiNetwork> nets, int ch) =>
        nets.Where(n => n.Band == "2.4GHz")
            .Sum(n =>
            {
                int diff = Math.Abs(n.Channel - ch);
                if (diff > 4) return 0.0;
                // 겹칠수록 높은 점수 (신호세기 가중)
                return n.Signal * (5 - diff) / 5.0;
            });

    // 5GHz: 20MHz → 같은 채널만 겹침 (채널 간격 4)
    private static double InterferenceScore5(IReadOnlyList<WifiNetwork> nets, int ch) =>
        nets.Sum(n =>
        {
            int diff = Math.Abs(n.Channel - ch);
            return diff == 0 ? n.Signal : diff <= 4 ? n.Signal * 0.2 : 0.0;
        });
}
