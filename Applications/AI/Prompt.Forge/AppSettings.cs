namespace Prompt.Forge;

public sealed class AppSettings
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Prompt.Forge", "sync.json");

    public string DbPath    { get; set; } = "";
    public string GithubPat { get; set; } = "";
    public string GistId    { get; set; } = "";

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(this,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json, System.Text.Encoding.UTF8);
    }

    public string ResolvedDbPath => string.IsNullOrWhiteSpace(DbPath)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Prompt.Forge", "prompts.db")
        : DbPath;
}
