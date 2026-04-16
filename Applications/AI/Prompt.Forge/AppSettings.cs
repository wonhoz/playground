using System.Security.Cryptography;

namespace Prompt.Forge;

public sealed class AppSettings
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Prompt.Forge", "sync.json");

    public string DbPath          { get; set; } = "";
    public string GithubPatEnc   { get; set; } = "";  // DPAPI 암호화된 PAT (Base64)
    public string GistId         { get; set; } = "";
    public string LastSortOrder  { get; set; } = "updated";  // updated / use_count / custom
    public int    LastSelectedId { get; set; } = -1;         // 마지막 선택 프롬프트 ID
    public List<string> RecentSearches { get; set; } = [];
    public List<FilterPreset> FilterPresets { get; set; } = [];

    // 창 위치/크기 — double.NaN은 저장 없음 (CenterScreen 사용)
    public double WindowLeft   { get; set; } = double.NaN;
    public double WindowTop    { get; set; } = double.NaN;
    public double WindowWidth  { get; set; } = 1000;
    public double WindowHeight { get; set; } = 680;
    public string WindowState  { get; set; } = "Normal";

    // 런타임에서 사용하는 평문 PAT (직렬화 제외)
    [System.Text.Json.Serialization.JsonIgnore]
    public string GithubPat
    {
        get => Decrypt(GithubPatEnc);
        set => GithubPatEnc = Encrypt(value);
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            // 기존 평문 PAT 마이그레이션 (GithubPat 키가 있던 이전 버전 호환)
            MigrateLegacyPat(settings, json);
            return settings;
        }
        catch { return new(); }
    }

    static void MigrateLegacyPat(AppSettings settings, string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("GithubPat", out var patProp) ||
                doc.RootElement.TryGetProperty("githubPat", out patProp))
            {
                var plainPat = patProp.GetString() ?? "";
                if (!string.IsNullOrEmpty(plainPat) && string.IsNullOrEmpty(settings.GithubPatEnc))
                {
                    settings.GithubPat = plainPat;
                    settings.Save();
                }
            }
        }
        catch { }
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

    static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch { return ""; }
    }

    static string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return "";
        try
        {
            var bytes = Convert.FromBase64String(encryptedBase64);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch { return ""; }
    }
}

public sealed record FilterPreset(
    string Name,
    string? Tag,
    string? Service,
    bool FavOnly,
    string? Search = null);
