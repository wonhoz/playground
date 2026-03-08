namespace Color.Grade.Services;

/// <summary>내장 알고리즘 프리셋 정의</summary>
public static class LutLibrary
{
    public static IReadOnlyList<LutInfo> BuiltIn { get; } = Build();

    static List<LutInfo> Build() =>
    [
        new LutInfo { Name = "원본 (없음)", IsBuiltIn = true,
            AlgoApply = (r, g, b) => (r, g, b) },

        new LutInfo { Name = "웜 (따뜻한)", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                return (Math.Min(1f, r * 1.10f + 0.02f),
                        g,
                        Math.Max(0f, b * 0.88f - 0.02f));
            }},

        new LutInfo { Name = "쿨 (차가운)", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                return (Math.Max(0f, r * 0.88f - 0.02f),
                        g,
                        Math.Min(1f, b * 1.10f + 0.02f));
            }},

        new LutInfo { Name = "빈티지", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                // 블랙 리프트 + 따뜻한 하이라이트
                float nr = r * 0.85f + 0.06f + (1f - r) * 0.02f;
                float ng = g * 0.82f + 0.05f;
                float nb = b * 0.76f + 0.08f;
                return (Math.Clamp(nr, 0f, 1f), Math.Clamp(ng, 0f, 1f), Math.Clamp(nb, 0f, 1f));
            }},

        new LutInfo { Name = "흑백 (B&W)", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                float lum = r * 0.299f + g * 0.587f + b * 0.114f;
                return (lum, lum, lum);
            }},

        new LutInfo { Name = "B&W 하이컨트", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                float lum = r * 0.299f + g * 0.587f + b * 0.114f;
                lum = Math.Clamp((lum - 0.5f) * 1.4f + 0.5f, 0f, 1f);
                return (lum, lum, lum);
            }},

        new LutInfo { Name = "틸 & 오렌지", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                float nr = Math.Min(1f, r * 1.18f);
                float ng = Math.Max(0f, g * 0.92f - 0.02f);
                float nb = Math.Max(0f, b * 0.78f + 0.08f * (1f - b));
                return (nr, ng, nb);
            }},

        new LutInfo { Name = "팝 (채도 강조)", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                float lum  = r * 0.299f + g * 0.587f + b * 0.114f;
                float factor = 1.35f;
                return (Math.Clamp(lum + (r - lum) * factor, 0f, 1f),
                        Math.Clamp(lum + (g - lum) * factor, 0f, 1f),
                        Math.Clamp(lum + (b - lum) * factor, 0f, 1f));
            }},

        new LutInfo { Name = "시네마틱", IsBuiltIn = true,
            AlgoApply = (r, g, b) =>
            {
                // 어두운 톤 + 틸 쉐도우 + 오렌지 스킨
                float nr = r * 0.95f + b * 0.05f;
                float ng = g * 0.90f;
                float nb = b * 1.05f + r * 0.02f;
                // 약간 페이드
                nr = nr * 0.92f + 0.04f;
                ng = ng * 0.92f + 0.04f;
                nb = nb * 0.92f + 0.04f;
                return (Math.Clamp(nr, 0f, 1f), Math.Clamp(ng, 0f, 1f), Math.Clamp(nb, 0f, 1f));
            }},
    ];
}
