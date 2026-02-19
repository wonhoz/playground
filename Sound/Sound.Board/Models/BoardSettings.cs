namespace SoundBoard.Models;

public class BoardSettings
{
    public List<SoundButton> Buttons      { get; set; } = [];
    public float             Volume       { get; set; } = 0.8f;
    public bool              OverlapSounds { get; set; } = true;

    public static BoardSettings CreateDefault() => new()
    {
        Buttons =
        [
            new() { Name = "Air Horn",    Emoji = "📯", BuiltInKey = "airhorn",  Color = "#C0392B" },
            new() { Name = "Applause",    Emoji = "👏", BuiltInKey = "applause", Color = "#27AE60" },
            new() { Name = "Rimshot",     Emoji = "🥁", BuiltInKey = "rimshot",  Color = "#D35400" },
            new() { Name = "Sad Trombone",Emoji = "😢", BuiltInKey = "sad",      Color = "#2980B9" },
            new() { Name = "Ding",        Emoji = "🔔", BuiltInKey = "ding",     Color = "#8E44AD" },
            new() { Name = "Laser",       Emoji = "⚡", BuiltInKey = "laser",    Color = "#16A085" },
            new() { Name = "Boom",        Emoji = "💥", BuiltInKey = "boom",     Color = "#E67E22" },
            new() { Name = "Fanfare",     Emoji = "🎺", BuiltInKey = "fanfare",  Color = "#1ABC9C" },
        ]
    };
}
