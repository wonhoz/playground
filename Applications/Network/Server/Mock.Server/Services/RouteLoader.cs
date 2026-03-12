using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MockServer.Services;

public static class RouteLoader
{
    private static readonly IDeserializer _yamlDe =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    private static readonly ISerializer _yamlSer =
        new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

    // ── 기존: RouteConfig 기반 ──────────────────────────────────
    public static RouteConfig LoadYaml(string yaml) =>
        _yamlDe.Deserialize<RouteConfig>(yaml) ?? new();

    public static RouteConfig LoadJson(string json) =>
        JsonSerializer.Deserialize<RouteConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

    public static string ToYaml(RouteConfig cfg) => _yamlSer.Serialize(cfg);

    public static string ToJson(RouteConfig cfg) =>
        JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });

    // ── 신규: MockRoute 기반 (GUI ↔ 에디터 변환) ───────────────
    public static List<MockRoute> LoadRoutesYaml(string yaml)
    {
        try
        {
            var cfg = _yamlDe.Deserialize<RouteConfig>(yaml) ?? new();
            return cfg.Routes.Select(MockRoute.FromEntry).ToList();
        }
        catch { return []; }
    }

    public static List<MockRoute> LoadRoutesJson(string json)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<RouteConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return cfg.Routes.Select(MockRoute.FromEntry).ToList();
        }
        catch { return []; }
    }

    public static string ToYaml(IEnumerable<MockRoute> routes)
    {
        var cfg = new RouteConfig { Routes = routes.Select(r => r.ToEntry()).ToList() };
        return _yamlSer.Serialize(cfg);
    }

    public static string ToJson(IEnumerable<MockRoute> routes)
    {
        var cfg = new RouteConfig { Routes = routes.Select(r => r.ToEntry()).ToList() };
        return JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── 유효성 검사 ─────────────────────────────────────────────
    public static bool Validate(string text, bool isYaml, out string error)
    {
        try
        {
            if (isYaml) LoadYaml(text);
            else        LoadJson(text);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string DefaultYaml =>
        """
        routes:
          - method: GET
            path: /api/users
            status: 200
            delay: 0
            description: 사용자 목록 반환
            response: |
              {"users":[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]}

          - method: POST
            path: /api/users
            status: 201
            delay: 100
            description: 사용자 생성 (100ms 지연)
            response: |
              {"id":3,"name":"New User","created":true}

          - method: GET
            path: /api/status
            status: 200
            delay: 0
            description: 서버 상태 확인
            response: |
              {"status":"ok","version":"1.0.0"}
        """;
}
