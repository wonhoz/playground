using System.Text.Json;

namespace FolderPurge.Services;

public class AppSettings
{
    public List<string> TargetFolders { get; set; } = [];
    public bool ScanEmptyFolders { get; set; } = true;
    public bool ScanVsArtifacts { get; set; } = true;
    public bool ScanEmptyFiles { get; set; } = false;
    public bool UseRecycleBin { get; set; } = true;
    public bool PreviewOnly { get; set; } = false;
    public List<string> ExcludedFolders { get; set; } =
        [".git", ".vs", ".svn", ".hg", "node_modules", "__pycache__"];
    public List<string> VsArtifactFolderNames { get; set; } = ["bin", "obj"];
    public List<string> VsArtifactFileExtensions { get; set; } = [".user"];

    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FolderPurge", "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch { /* 손상된 설정 무시, 기본값 사용 */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, _opts));
        }
        catch { /* 저장 실패 무시 */ }
    }
}
