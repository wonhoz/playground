namespace AIPForge.Services;

public enum DiffKind { Same, Added, Removed }

public sealed record DiffLine(DiffKind Kind, int? OldNo, int? NewNo, string Text);

/// <summary>
/// 줄 단위 비교 — 공통 앞뒤를 잘라낸 뒤 남은 구간만 LCS로 비교해
/// 대용량 AIP(수천 줄)에서도 일반적인 편집은 즉시 계산된다.
/// </summary>
public static class LineDiff
{
    private const int MaxLcsCells = 25_000_000;

    public static List<DiffLine> Compute(string oldText, string newText)
    {
        var a = Split(oldText);
        var b = Split(newText);

        int start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start]) start++;
        int endA = a.Length, endB = b.Length;
        while (endA > start && endB > start && a[endA - 1] == b[endB - 1]) { endA--; endB--; }

        var result = new List<DiffLine>();
        for (var i = 0; i < start; i++) result.Add(new DiffLine(DiffKind.Same, i + 1, i + 1, a[i]));

        int n = endA - start, m = endB - start;
        if ((long)n * m > MaxLcsCells)
        {
            // 변경 구간이 너무 크면 단순 치환으로 표시
            for (var i = 0; i < n; i++) result.Add(new DiffLine(DiffKind.Removed, start + i + 1, null, a[start + i]));
            for (var j = 0; j < m; j++) result.Add(new DiffLine(DiffKind.Added, null, start + j + 1, b[start + j]));
        }
        else
        {
            var lcs = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
                for (var j = m - 1; j >= 0; j--)
                    lcs[i, j] = a[start + i] == b[start + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

            int x = 0, y = 0;
            while (x < n || y < m)
            {
                if (x < n && y < m && a[start + x] == b[start + y])
                {
                    result.Add(new DiffLine(DiffKind.Same, start + x + 1, start + y + 1, a[start + x]));
                    x++; y++;
                }
                else if (x < n && (y == m || lcs[x + 1, y] >= lcs[x, y + 1]))
                {
                    // 삭제 줄을 추가 줄보다 먼저 보여준다 (일반적인 diff 순서)
                    result.Add(new DiffLine(DiffKind.Removed, start + x + 1, null, a[start + x]));
                    x++;
                }
                else
                {
                    result.Add(new DiffLine(DiffKind.Added, null, start + y + 1, b[start + y]));
                    y++;
                }
            }
        }

        for (int i = endA, j = endB; i < a.Length; i++, j++)
            result.Add(new DiffLine(DiffKind.Same, i + 1, j + 1, a[i]));
        return result;
    }

    /// <summary>변경 줄 주변 <paramref name="context"/>줄만 남기고 나머지는 생략 표시로 접는다</summary>
    public static List<DiffLine?> WithContext(List<DiffLine> lines, int context = 3)
    {
        var keep = new bool[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Kind == DiffKind.Same) continue;
            for (var k = Math.Max(0, i - context); k <= Math.Min(lines.Count - 1, i + context); k++) keep[k] = true;
        }
        var result = new List<DiffLine?>();
        var skipping = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (keep[i]) { result.Add(lines[i]); skipping = false; }
            else if (!skipping) { result.Add(null); skipping = true; }
        }
        return result;
    }

    private static string[] Split(string text) => text.Replace("\r\n", "\n").Split('\n');
}
