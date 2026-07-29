using System.Text;
using SiegeLab.Models;

namespace SiegeLab.Services;

/// <summary>점검 결과를 Markdown 리포트로 내보낸다.</summary>
public static class ReportExporter
{
    public static string BuildMarkdown(string target, IReadOnlyList<SecurityFinding> findings,
        LoadSnapshot? load)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Siege.Lab 점검 리포트");
        sb.AppendLine();
        sb.AppendLine($"- 대상: `{target}`");
        sb.AppendLine($"- 생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (KST)");

        if (findings.Count > 0)
        {
            var (score, grade) = SecurityAuditor.Score(findings);
            sb.AppendLine($"- 보안 점수: **{score}/100 ({grade})**");
            sb.AppendLine();
            sb.AppendLine("## 보안 점검 결과");
            sb.AppendLine();
            sb.AppendLine("| 심각도 | 카테고리 | 항목 | 권고 |");
            sb.AppendLine("|--------|----------|------|------|");
            foreach (var f in findings.OrderByDescending(x => (int)x.Severity == 5 ? -1 : (int)x.Severity))
            {
                sb.AppendLine($"| {f.SeverityLabel} | {f.CategoryLabel} | " +
                              $"{Esc(f.Title)} | {Esc(f.Recommendation)} |");
            }
            sb.AppendLine();
        }

        if (load != null)
        {
            sb.AppendLine("## 부하 테스트 결과");
            sb.AppendLine();
            sb.AppendLine($"- 총 요청: {load.Total:N0} (성공 {load.Success:N0} / 실패 {load.Failed:N0})");
            sb.AppendLine($"- 평균 RPS: {load.AvgRps:F1}");
            sb.AppendLine($"- 지연시간(ms): avg {load.LatAvg:F0} · p50 {load.LatP50:F0} · " +
                          $"p95 {load.LatP95:F0} · p99 {load.LatP99:F0} · max {load.LatMax:F0}");
            sb.AppendLine($"- 처리량: {load.MbPerSec:F2} MB/s");
            sb.AppendLine();
            if (load.StatusDist.Count > 0)
            {
                sb.AppendLine("### 상태코드 분포");
                foreach (var kv in load.StatusDist.OrderBy(k => k.Key))
                    sb.AppendLine($"- {kv.Key}: {kv.Value:N0}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine("_Siege.Lab — 인가된 대상에 대한 방어 관점 점검 도구_");
        return sb.ToString();
    }

    private static string Esc(string s) => s.Replace("|", "\\|").Replace("\n", " ");
}
