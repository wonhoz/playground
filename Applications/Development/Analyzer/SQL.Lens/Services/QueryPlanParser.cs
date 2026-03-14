namespace SqlLens.Services;

public record PlanNode(
    int Id,
    int ParentId,
    int NotUsed,
    string Detail,
    int Depth = 0
);

public record PlanIssue(string Severity, string Message, string Suggestion);

public static class QueryPlanParser
{
    /// <summary>
    /// EXPLAIN QUERY PLAN 결과를 트리 노드 목록으로 변환
    /// </summary>
    public static List<PlanNode> BuildTree(List<(int id, int parent, int notused, string detail)> rows)
    {
        var nodes = rows.Select(r => new PlanNode(r.id, r.parent, r.notused, r.detail)).ToList();
        // 깊이 계산
        var depthMap = new Dictionary<int, int>();
        foreach (var row in rows)
            depthMap[row.id] = CalcDepth(row, rows);
        return nodes.Select(n => n with { Depth = depthMap.GetValueOrDefault(n.Id) }).ToList();
    }

    static int CalcDepth((int id, int parent, int notused, string detail) node,
        List<(int id, int parent, int notused, string detail)> all, int d = 0)
    {
        if (node.parent == 0) return d;
        var parent = all.FirstOrDefault(r => r.id == node.parent);
        if (parent.id == 0 && parent.detail == null) return d;
        return CalcDepth(parent, all, d + 1);
    }

    /// <summary>
    /// 실행 계획에서 성능 이슈 탐지
    /// </summary>
    public static List<PlanIssue> DetectIssues(List<PlanNode> nodes)
    {
        var issues = new List<PlanIssue>();
        foreach (var node in nodes)
        {
            string d = node.Detail.ToUpper();

            if (d.Contains("SCAN TABLE") && !d.Contains("COVERING INDEX"))
                issues.Add(new PlanIssue("⚠️ 경고",
                    $"전체 테이블 스캔: {node.Detail}",
                    "WHERE 절의 컬럼에 인덱스를 추가하면 성능이 크게 향상됩니다."));

            if (d.Contains("USE TEMP B-TREE FOR ORDER BY"))
                issues.Add(new PlanIssue("⚠️ 경고",
                    "임시 B-TREE 정렬 사용",
                    "ORDER BY 컬럼에 인덱스를 추가하면 정렬 없이 인덱스를 활용할 수 있습니다."));

            if (d.Contains("USE TEMP B-TREE FOR GROUP BY"))
                issues.Add(new PlanIssue("⚠️ 경고",
                    "임시 B-TREE GROUP BY 사용",
                    "GROUP BY 컬럼에 인덱스를 추가하면 성능이 향상됩니다."));

            if (d.Contains("SEARCH TABLE") && d.Contains("USING INDEX"))
                issues.Add(new PlanIssue("✅ 양호",
                    $"인덱스 사용: {node.Detail}",
                    "인덱스를 잘 활용하고 있습니다."));

            if (d.Contains("CORRELATED SCALAR SUBQUERY"))
                issues.Add(new PlanIssue("🔴 위험",
                    "상관 서브쿼리 발견",
                    "상관 서브쿼리는 행마다 실행됩니다. JOIN으로 변환을 검토하세요."));

            if (d.Contains("MATERIALIZE"))
                issues.Add(new PlanIssue("⚠️ 경고",
                    "임시 결과 구체화",
                    "WITH 절(CTE)이 여러 번 참조되면 구체화됩니다. 필요 시 인라인 뷰로 변환하세요."));
        }
        return issues;
    }
}
