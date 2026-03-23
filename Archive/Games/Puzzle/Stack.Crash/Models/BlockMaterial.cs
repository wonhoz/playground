namespace StackCrash.Models;

public enum BlockMaterial
{
    Wood,
    Stone,
    Metal,
    Ice,
    Glass,
    Explosive
}

public static class Materials
{
    public record Def(
        string    Name,
        float     Density,
        float     Friction,
        float     Restitution,
        string    Fill,
        string    Stroke,
        string    FillDark,   // 어두운 면 색상
        int       MaxHp       // 충격으로 파괴되는 HP
    );

    public static readonly IReadOnlyDictionary<BlockMaterial, Def> All =
        new Dictionary<BlockMaterial, Def>
        {
            [BlockMaterial.Wood]      = new("나무",   0.6f,  0.7f, 0.15f, "#C8883A", "#7A4A18", "#A06828", 3),
            [BlockMaterial.Stone]     = new("돌",     2.5f,  0.6f, 0.10f, "#7A7A8C", "#454552", "#5A5A6A", 5),
            [BlockMaterial.Metal]     = new("금속",   4.0f,  0.4f, 0.05f, "#7898B8", "#3A5878", "#5878A0", 8),
            [BlockMaterial.Ice]       = new("얼음",   0.9f,  0.1f, 0.30f, "#B8E0F0", "#7ABCDC", "#98CCDC", 2),
            [BlockMaterial.Glass]     = new("유리",   0.5f,  0.3f, 0.20f, "#C8E8FF", "#88C8F0", "#A8D8FF", 1),
            [BlockMaterial.Explosive] = new("폭발물", 1.5f,  0.5f, 0.05f, "#E85040", "#901818", "#C03020", 2),
        };

    public static Def Get(BlockMaterial m) => All[m];
}
