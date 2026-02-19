namespace QuickLauncher.Models;

public class CustomItem
{
    public string Name      { get; set; } = "";
    public string Target    { get; set; } = "";
    public bool   IsSnippet { get; set; }
}

public class LauncherSettings
{
    public int    HotkeyMods { get; set; } = 0x0001; // MOD_ALT
    public int    HotkeyVk   { get; set; } = 0x20;   // VK_SPACE
    public string HotkeyText { get; set; } = "Alt+Space";

    public List<CustomItem> CustomItems { get; set; } = [];
}
