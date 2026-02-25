namespace CodeSnap.Models;

public enum BackgroundType
{
    Gradient,
    Solid,
    Transparent
}

public record BackgroundPreset(string Name, string Color1, string Color2)
{
    public static readonly BackgroundPreset[] Gradients =
    [
        new("Violet Twilight", "#a78bfa", "#6366f1"),
        new("Pink Sunset",     "#f472b6", "#fb923c"),
        new("Ocean",           "#38bdf8", "#0ea5e9"),
        new("Forest",          "#4ade80", "#22c55e"),
        new("Night City",      "#1e1b4b", "#312e81"),
        new("Amber Glow",      "#F59E0B", "#D97706"),
    ];
}
