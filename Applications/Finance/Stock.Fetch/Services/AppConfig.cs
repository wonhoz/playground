using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>
/// 앱 설정 영속화. 저장 위치는 리포 밖 %LocalAppData%\Playground\Stock.Fetch\config.json
/// 이므로 KIS API 키가 소스 저장소에 절대 포함되지 않는다.
/// </summary>
public sealed class AppConfig
{
    // ── KIS Open API 자격(선택 — KIS 소스 사용 시에만 필요) ──
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>true=모의투자(:29443), false=실전(:9443). 시세 조회는 실전 권장.</summary>
    public bool UseMockServer { get; set; } = false;

    // ── 즐겨찾기 종목 ──
    public List<FavoriteStock> Favorites { get; set; } = new();

    // ── 최근 사용 값(재실행 시 마지막 선택 복원) ──
    public SourceKind LastSource { get; set; } = SourceKind.Naver;
    public string LastCode { get; set; } = "005930";
    public string LastName { get; set; } = string.Empty;
    public string LastFrom { get; set; } = string.Empty;
    public string LastTo { get; set; } = string.Empty;
    public ExportFormat LastFormat { get; set; } = ExportFormat.Csv;
    /// <summary>마지막 선택 컬럼(빈 목록이면 전체).</summary>
    public List<CandleColumn> LastColumns { get; set; } = new();
    public bool LastIncludeHeader { get; set; } = true;
    public string LastExportDir { get; set; } = string.Empty;

    // ── 캐시된 OAuth 토큰(만료 전까지 재사용) ──
    public string CachedToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; } = DateTime.MinValue;

    [JsonIgnore] public bool HasKisCredentials => !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(AppSecret);

    // ──────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Stock.Fetch");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null) return cfg;
            }
        }
        catch { /* 손상된 설정은 기본값으로 폴백 */ }

        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }
}
