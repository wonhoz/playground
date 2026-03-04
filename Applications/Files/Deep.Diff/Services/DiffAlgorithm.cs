namespace DeepDiff.Services;

/// <summary>
/// Myers diff 알고리즘 구현 (O(ND) 시간복잡도)
/// </summary>
public static class DiffAlgorithm
{
    public enum EditType { Equal, Insert, Delete }

    public record EditOp(EditType Type, int LeftIndex, int RightIndex);

    // 문자열 배열에 대한 diff
    public static List<EditOp> Diff(IList<string> left, IList<string> right,
        StringComparison comparison = StringComparison.Ordinal)
        => DiffCore(left, right, (a, b) => string.Equals(a, b, comparison));

    // 바이트 배열에 대한 diff
    public static List<EditOp> DiffBytes(IList<byte> left, IList<byte> right)
        => DiffCore(left, right, (a, b) => a == b);

    private static List<EditOp> DiffCore<T>(IList<T> left, IList<T> right, Func<T, T, bool> eq)
    {
        int n = left.Count, m = right.Count;
        if (n == 0 && m == 0) return [];

        // LCS 기반 diff (Myers 알고리즘 - 최단 편집 경로)
        int max = n + m;
        if (max == 0) return [];

        var V = new int[2 * max + 2];
        var trace = new List<int[]>();

        for (int d = 0; d <= max; d++)
        {
            trace.Add((int[])V.Clone());
            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && V[k - 1 + max] < V[k + 1 + max]))
                    x = V[k + 1 + max];
                else
                    x = V[k - 1 + max] + 1;

                int y = x - k;
                while (x < n && y < m && eq(left[x], right[y])) { x++; y++; }
                V[k + max] = x;

                if (x >= n && y >= m)
                {
                    trace.Add((int[])V.Clone());
                    return Backtrack(trace, left, right, eq, max);
                }
            }
        }
        return Backtrack(trace, left, right, eq, max);
    }

    private static List<EditOp> Backtrack<T>(List<int[]> trace, IList<T> left, IList<T> right,
        Func<T, T, bool> eq, int max)
    {
        var ops = new List<EditOp>();
        int x = left.Count, y = right.Count;

        for (int d = trace.Count - 1; d > 0 && (x > 0 || y > 0); d--)
        {
            var v = trace[d - 1];
            int k = x - y;

            int prevK;
            if (k == -(d - 1) || (k != (d - 1) && v[k - 1 + max] < v[k + 1 + max]))
                prevK = k + 1;
            else
                prevK = k - 1;

            int prevX = v[prevK + max];
            int prevY = prevX - prevK;

            // 대각선 이동 (equal)
            while (x > prevX && y > prevY)
            {
                ops.Add(new(EditType.Equal, x - 1, y - 1));
                x--; y--;
            }

            if (d > 0)
            {
                if (x == prevX) ops.Add(new(EditType.Insert, -1, y - 1));  // insert from right
                else ops.Add(new(EditType.Delete, x - 1, -1));              // delete from left
                x = prevX; y = prevY;
            }
        }

        // 나머지 equal
        while (x > 0 && y > 0 && eq(left[x - 1], right[y - 1]))
        {
            ops.Add(new(EditType.Equal, x - 1, y - 1));
            x--; y--;
        }
        while (x > 0) { ops.Add(new(EditType.Delete, x - 1, -1)); x--; }
        while (y > 0) { ops.Add(new(EditType.Insert, -1, y - 1)); y--; }

        ops.Reverse();
        return ops;
    }

    /// <summary>문자 수준 diff (인라인 하이라이트용)</summary>
    public static List<EditOp> DiffChars(string left, string right)
        => DiffCore(left.ToCharArray(), right.ToCharArray(), (a, b) => a == b);
}
