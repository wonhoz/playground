using System.Windows.Media;

namespace SiegeLab.Models;

/// <summary>점검 결과 심각도.</summary>
public enum Severity
{
    Info,
    Low,
    Medium,
    High,
    Critical,
    Ok
}

public static class SeverityExtensions
{
    public static string Label(this Severity s) => s switch
    {
        Severity.Critical => "치명",
        Severity.High     => "높음",
        Severity.Medium   => "중간",
        Severity.Low      => "낮음",
        Severity.Ok       => "양호",
        _                 => "정보"
    };

    public static Brush Brush(this Severity s)
    {
        var key = s switch
        {
            Severity.Critical => "CriticalBrush",
            Severity.High     => "HighBrush",
            Severity.Medium   => "MediumBrush",
            Severity.Low      => "LowBrush",
            Severity.Ok       => "OkBrush",
            _                 => "FgDimBrush"
        };
        return (Brush)System.Windows.Application.Current.Resources[key];
    }

    /// <summary>보안 점수 가중치 (감점).</summary>
    public static int Weight(this Severity s) => s switch
    {
        Severity.Critical => 25,
        Severity.High     => 12,
        Severity.Medium   => 6,
        Severity.Low      => 2,
        _                 => 0
    };
}

/// <summary>점검 카테고리.</summary>
public enum FindingCategory
{
    Tls,
    Headers,
    Cookies,
    Disclosure,
    Cors,
    Methods,
    Exposure,   // 노출·백도어
    Availability
}

/// <summary>보안/취약점 점검 결과 항목.</summary>
public sealed class SecurityFinding
{
    public FindingCategory Category { get; init; }
    public Severity Severity { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Recommendation { get; init; } = "";

    public string CategoryLabel => Category switch
    {
        FindingCategory.Tls          => "TLS/인증서",
        FindingCategory.Headers      => "보안 헤더",
        FindingCategory.Cookies      => "쿠키",
        FindingCategory.Disclosure   => "정보 노출",
        FindingCategory.Cors         => "CORS",
        FindingCategory.Methods      => "HTTP 메서드",
        FindingCategory.Exposure     => "노출·백도어",
        FindingCategory.Availability => "가용성",
        _                            => "기타"
    };

    public string SeverityLabel => Severity.Label();
    public Brush SeverityBrush => Severity.Brush();
}

/// <summary>저장되는 대상 프로파일(허용 목록 항목).</summary>
public sealed class TargetProfile
{
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string AuthHeader { get; set; } = "";   // 예: "Authorization: Bearer xxx"
    public bool Authorized { get; set; }            // 소유·허가 확인 서약
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>부하 테스트 설정.</summary>
public sealed class LoadTestConfig
{
    public string Url { get; set; } = "";
    public string Method { get; set; } = "GET";
    public int Concurrency { get; set; } = 20;      // 동시 가상 사용자
    public int DurationSeconds { get; set; } = 30;
    public int RampUpSeconds { get; set; } = 5;
    public int MaxRps { get; set; } = 0;            // 0 = 무제한(안전 상한 별도 적용)
    public int TimeoutMs { get; set; } = 10_000;
    public string Body { get; set; } = "";
    public string ContentType { get; set; } = "application/json";
    public string ExtraHeader { get; set; } = "";
}
