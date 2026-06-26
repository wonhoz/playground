using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Watch.Conditions;
using Stock.Watch.Models;

namespace Stock.Watch.Services;

/// <summary>
/// 앱 설정 + 관심종목/조건 영속화. 저장 위치는 리포 밖 %LocalAppData%\Playground\Stock.Watch\config.json
/// 이므로 API 키/Slack URL이 소스 저장소에 절대 포함되지 않는다.
/// </summary>
public sealed class AppConfig
{
    // ── KIS Open API 자격 ──
    public string AppKey { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>true=모의투자(:29443), false=실전(:9443).</summary>
    public bool UseMockServer { get; set; } = true;

    // ── 알림 ──
    public string SlackWebhookUrl { get; set; } = string.Empty;

    // ── 실시간(WebSocket) ──
    /// <summary>WebSocket 실시간 체결가 사용. 폴링과 병행(폴링=일봉/지표 기준선, 실시간=틱 가격).</summary>
    public bool UseRealtime { get; set; } = true;

    // ── 폴링 ──
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>장중 시간에만 폴링(09:00~15:30 KST). false면 항상 폴링.</summary>
    public bool MarketHoursOnly { get; set; } = true;

    /// <summary>같은 종목·방향 알림 최소 간격(초). 도배 방지.</summary>
    public int AlertCooldownSeconds { get; set; } = 600;

    public List<WatchedStock> Watchlist { get; set; } = new();

    // ── 캐시된 OAuth 토큰(만료 전까지 재사용) ──
    public string CachedToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; } = DateTime.MinValue;

    // ── 캐시된 WebSocket approval_key ──
    public string CachedApprovalKey { get; set; } = string.Empty;
    public DateTime ApprovalExpiresAt { get; set; } = DateTime.MinValue;

    [JsonIgnore] public bool HasCredentials => !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(AppSecret);

    // ──────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Stock.Watch");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg != null)
                {
                    if (cfg.Watchlist.Count == 0) cfg.Watchlist = DefaultWatchlist();
                    return cfg;
                }
            }
        }
        catch { /* 손상된 설정은 기본값으로 폴백 */ }

        return new AppConfig { Watchlist = DefaultWatchlist() };
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    /// <summary>최초 실행 시 기본 4종목 + 대표 조건 프리셋.</summary>
    public static List<WatchedStock> DefaultWatchlist() => new()
    {
        MakeDefault("000660", "SK하이닉스"),
        MakeDefault("402340", "SK스퀘어"),
        MakeDefault("000890", "보해양조"),
        MakeDefault("026940", "부국철강"),
    };

    /// <summary>기본 매수/매도 조건 프리셋이 채워진 새 종목 생성(신규 추가 시 재사용).</summary>
    public static WatchedStock MakeDefault(string code, string name) => new()
    {
        Code = code,
        Name = name,
        BuyRules = new RuleSet
        {
            Kind = RuleKind.Buy,
            Combine = CombineMode.Any,
            Conditions =
            {
                new Condition { Left = Operand.Rsi14, Op = CompareOp.LessThan, RightType = RightKind.Constant, RightValue = 30 },
                new Condition { Left = Operand.Price, Op = CompareOp.LessOrEqual, RightType = RightKind.Indicator, RightOperand = Operand.BollLower, RightValue = 1 },
            }
        },
        SellRules = new RuleSet
        {
            Kind = RuleKind.Sell,
            Combine = CombineMode.Any,
            Conditions =
            {
                new Condition { Left = Operand.Rsi14, Op = CompareOp.GreaterThan, RightType = RightKind.Constant, RightValue = 70 },
                new Condition { Left = Operand.Price, Op = CompareOp.GreaterOrEqual, RightType = RightKind.Indicator, RightOperand = Operand.BollUpper, RightValue = 1 },
            }
        }
    };
}
