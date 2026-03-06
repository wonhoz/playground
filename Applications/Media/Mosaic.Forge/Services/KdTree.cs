namespace Mosaic.Forge.Services;

/// <summary>CIE Lab 3차원 k-d Tree — 최근접 타일 탐색</summary>
sealed class KdTree
{
    sealed class Node
    {
        public TileEntry Entry = null!;
        public Node?     Left, Right;
        public int       Axis;
    }

    Node? _root;

    public void Build(List<TileEntry> entries)
        => _root = BuildNode([.. entries], 0);

    static Node? BuildNode(TileEntry[] arr, int depth)
    {
        if (arr.Length == 0) return null;
        int axis = depth % 3;
        Array.Sort(arr, (a, b) => GetComp(a, axis).CompareTo(GetComp(b, axis)));
        int mid = arr.Length / 2;
        return new Node
        {
            Entry = arr[mid],
            Axis  = axis,
            Left  = BuildNode(arr[..mid],        depth + 1),
            Right = BuildNode(arr[(mid + 1)..],  depth + 1)
        };
    }

    static double GetComp(TileEntry e, int axis) => axis switch
    {
        0 => e.LabL,
        1 => e.LabA,
        _ => e.LabB
    };

    static double TargetComp(double L, double A, double B, int axis) => axis switch
    {
        0 => L,
        1 => A,
        _ => B
    };

    /// <summary>조건 filter를 만족하는 가장 가까운 타일 반환. 없으면 무조건 가장 가까운 타일.</summary>
    public TileEntry FindNearest(double L, double A, double B,
                                 Predicate<TileEntry>? filter = null)
    {
        TileEntry? best    = null;
        double     bestDist = double.MaxValue;
        SearchNearest(_root, L, A, B, ref best, ref bestDist, filter);

        if (best == null)
        {
            // filter가 모두 걸러낸 경우 → 제한 무시하고 재탐색
            SearchNearest(_root, L, A, B, ref best, ref bestDist, null);
        }

        return best!;
    }

    static void SearchNearest(Node? node, double L, double A, double B,
                               ref TileEntry? best, ref double bestDist,
                               Predicate<TileEntry>? filter)
    {
        if (node == null) return;

        if (filter == null || filter(node.Entry))
        {
            double dist = ColorSpace.DistanceSq(L, A, B,
                                                node.Entry.LabL,
                                                node.Entry.LabA,
                                                node.Entry.LabB);
            if (dist < bestDist)
            {
                bestDist = dist;
                best     = node.Entry;
            }
        }

        double tVal    = TargetComp(L, A, B, node.Axis);
        double nVal    = GetComp(node.Entry, node.Axis);
        double diff    = tVal - nVal;
        var    near    = diff < 0 ? node.Left : node.Right;
        var    far     = diff < 0 ? node.Right : node.Left;

        SearchNearest(near, L, A, B, ref best, ref bestDist, filter);
        if (diff * diff < bestDist)
            SearchNearest(far, L, A, B, ref best, ref bestDist, filter);
    }
}
