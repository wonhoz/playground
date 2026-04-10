using System.IO;
using System.Text.Json;

namespace CaseForge.Services;

public class AppSettings
{
    public uint         HotkeyModifiers   { get; set; } = 0x000C; // MOD_WIN | MOD_SHIFT
    public uint         HotkeyVK          { get; set; } = 0x43;   // C
    public List<string> PinnedCases       { get; set; } = [];
    public List<string> RecentHistory     { get; set; } = [];
    public bool         AutoLoadClipboard { get; set; } = true;
}

public static class SettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "CaseForge", "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts));
        }
        catch { }
    }

    // 기존 settings 객체에 이력 추가 — Load() 없이 파일 I/O 1회만 발생
    public static void AddHistory(AppSettings settings, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        settings.RecentHistory.Remove(text);
        settings.RecentHistory.Insert(0, text);
        if (settings.RecentHistory.Count > 10)
            settings.RecentHistory = settings.RecentHistory[..10];
        Save(settings);
    }

    // Win+Shift+C → "Win + Shift + C" 형태로 변환
    public static string FormatHotkey(uint mods, uint vk)
    {
        var parts = new List<string>();
        if ((mods & 0x0008) != 0) parts.Add("Win");
        if ((mods & 0x0004) != 0) parts.Add("Shift");
        if ((mods & 0x0002) != 0) parts.Add("Ctrl");
        if ((mods & 0x0001) != 0) parts.Add("Alt");
        parts.Add(((System.Windows.Forms.Keys)vk).ToString());
        return string.Join(" + ", parts);
    }
}
