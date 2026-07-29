using System.IO;
using System.Text.Json;
using SiegeLab.Models;

namespace SiegeLab.Services;

/// <summary>
/// 모든 능동 테스트의 안전 게이트.
/// 소유·허가가 확인된 호스트(허용 목록)만 대상으로 허용하고,
/// 동시성·RPS·지속시간에 하드 상한을 적용한다.
/// </summary>
public sealed class SafetyGate
{
    // ── 하드 상한(오조작으로 인한 과도한 부하 방지) ─────────────
    public const int MaxConcurrency = 500;
    public const int MaxRps         = 2_000;
    public const int MaxDurationSec = 3_600;

    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Siege.Lab");

    private static readonly string ProfilePath = Path.Combine(DataDir, "targets.json");

    private readonly List<TargetProfile> _profiles = [];

    public IReadOnlyList<TargetProfile> Profiles => _profiles;

    public SafetyGate() => Load();

    // ── 허용 목록 관리 ───────────────────────────────────────────
    public void Load()
    {
        _profiles.Clear();
        try
        {
            if (File.Exists(ProfilePath))
            {
                var json = File.ReadAllText(ProfilePath);
                var list = JsonSerializer.Deserialize<List<TargetProfile>>(json);
                if (list != null) _profiles.AddRange(list);
            }
        }
        catch { /* 손상 시 무시하고 빈 목록 */ }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var json = JsonSerializer.Serialize(_profiles,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProfilePath, json);
        }
        catch { /* 저장 실패 무시 */ }
    }

    public void AddOrUpdate(TargetProfile profile)
    {
        var host = HostOf(profile.BaseUrl);
        _profiles.RemoveAll(p => HostOf(p.BaseUrl).Equals(host, StringComparison.OrdinalIgnoreCase));
        _profiles.Add(profile);
        Save();
    }

    public void Remove(TargetProfile profile)
    {
        _profiles.Remove(profile);
        Save();
    }

    /// <summary>해당 URL의 호스트가 허가 서약된 허용 목록에 있는지.</summary>
    public bool IsAuthorized(string url)
    {
        var host = HostOf(url);
        if (string.IsNullOrEmpty(host)) return false;

        // 루프백은 항상 허용(로컬/스테이징 테스트).
        if (IsLoopback(host)) return true;

        return _profiles.Any(p =>
            p.Authorized &&
            HostOf(p.BaseUrl).Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>대상 검증. 통과하지 못하면 사유를 반환.</summary>
    public (bool ok, string reason) Validate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "대상 URL이 비어 있습니다.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return (false, "http(s) 절대 URL이 아닙니다.");

        if (!IsAuthorized(url))
            return (false,
                $"'{uri.Host}' 은(는) 허가된 대상이 아닙니다. " +
                "대상 탭에서 소유·허가를 서약한 프로파일을 먼저 등록하세요.");

        return (true, "");
    }

    // ── 상한 적용 ────────────────────────────────────────────────
    public static int ClampConcurrency(int v) => Math.Clamp(v, 1, MaxConcurrency);
    public static int ClampRps(int v)         => v <= 0 ? 0 : Math.Clamp(v, 1, MaxRps);
    public static int ClampDuration(int v)    => Math.Clamp(v, 1, MaxDurationSec);

    // ── 유틸 ────────────────────────────────────────────────────
    public static string HostOf(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) return uri.Host;
        return url.Trim();
    }

    private static bool IsLoopback(string host) =>
        host is "localhost" or "127.0.0.1" or "::1" ||
        host.StartsWith("127.", StringComparison.Ordinal) ||
        host.StartsWith("192.168.", StringComparison.Ordinal) ||
        host.StartsWith("10.", StringComparison.Ordinal);
}
