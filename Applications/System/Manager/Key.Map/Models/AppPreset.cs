namespace Key.Map.Models;

sealed class AppPreset
{
    public string              Name      { get; set; } = "";
    public List<ShortcutEntry> Shortcuts { get; set; } = [];
}
