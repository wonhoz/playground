namespace AIPForge.Services;

/// <summary>사용자 설정 — %LocalAppData%\Playground\AIP.Forge\settings.json</summary>
public sealed class AppSettings
{
    public string? AdvInstallerCliPath { get; set; }
    public List<string> RecentFiles { get; set; } = [];
    public bool RefreshSyncBeforeBuild { get; set; }
    public bool NewProductCodeOnVersionChange { get; set; } = true;
    public double LogPanelHeight { get; set; } = 220;
    public double NavWidth { get; set; } = 300;

    private const int MaxRecent = 12;

    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Playground", "AIP.Forge");

    private static string FilePath => Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // 손상된 설정은 무시하고 기본값 사용
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(DataDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // 설정 저장 실패는 앱 동작에 영향 없음
        }
    }

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecent) RecentFiles.RemoveRange(MaxRecent, RecentFiles.Count - MaxRecent);
    }
}
