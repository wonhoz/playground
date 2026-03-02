namespace DiskLens.Services;

/// <summary>Squarified Treemap 알고리즘 (Bruls et al.)</summary>
public static class TreemapLayout
{
    private const int PAD = 1; // 셀 간 여백

    /// <summary>FileNode 트리를 화면 블록 목록으로 변환</summary>
    public static List<TreemapBlock> Build(FileNode root, Rect bounds, int maxDepth = 2)
    {
        var result = new List<TreemapBlock>();
        if (root.Size == 0 || bounds.Width < 2 || bounds.Height < 2) return result;
        Squarify(root.Children, bounds, root.Size, 0, maxDepth, result);
        return result;
    }

    private static void Squarify(
        List<FileNode> nodes, Rect bounds, long parentSize,
        int depth, int maxDepth, List<TreemapBlock> result)
    {
        if (nodes.Count == 0 || bounds.Width < 2 || bounds.Height < 2) return;

        var sorted = nodes.Where(n => n.Size > 0).OrderByDescending(n => n.Size).ToList();
        if (sorted.Count == 0) return;

        double totalArea = bounds.Width * bounds.Height;
        double scale     = totalArea / parentSize;
        var areas = sorted.Select(n => n.Size * scale).ToList();

        LayoutSquarified(sorted, areas, bounds, depth, maxDepth, result);
    }

    private static void LayoutSquarified(
        List<FileNode> nodes, List<double> areas, Rect bounds,
        int depth, int maxDepth, List<TreemapBlock> result)
    {
        if (nodes.Count == 0) return;

        // 현재 처리할 인덱스
        int idx = 0;
        var cur = bounds;

        while (idx < nodes.Count)
        {
            if (cur.Width < 1 || cur.Height < 1) break;

            bool horiz = cur.Width >= cur.Height;
            double strip = horiz ? cur.Height : cur.Width; // 단변 길이

            // row에 포함할 최적 아이템 수 찾기
            int best = 1;
            double bestWorst = WorstRatio(areas, idx, 1, strip);
            for (int k = 2; idx + k <= nodes.Count; k++)
            {
                double w = WorstRatio(areas, idx, k, strip);
                if (w <= bestWorst) { bestWorst = w; best = k; }
                else break;
            }

            PlaceRow(nodes, areas, idx, best, cur, horiz, strip, depth, maxDepth, result, out cur);
            idx += best;
        }
    }

    private static void PlaceRow(
        List<FileNode> nodes, List<double> areas,
        int start, int count, Rect bounds, bool horiz, double strip,
        int depth, int maxDepth, List<TreemapBlock> result, out Rect leftover)
    {
        double rowArea = 0;
        for (int i = start; i < start + count; i++) rowArea += areas[i];
        double stripThick = rowArea / strip; // 장변 방향 두께

        double offset = 0;
        for (int i = start; i < start + count; i++)
        {
            double cellLen = areas[i] / stripThick; // 단변 방향 길이

            Rect cell;
            if (horiz) // 스트립이 위→아래로 배치, 셀은 좌→우
                cell = new Rect(bounds.X + offset, bounds.Y, cellLen, stripThick);
            else        // 스트립이 좌→우로 배치, 셀은 위→아래
                cell = new Rect(bounds.X, bounds.Y + offset, stripThick, cellLen);

            offset += cellLen;

            var display = new Rect(
                cell.X + PAD, cell.Y + PAD,
                Math.Max(0, cell.Width  - PAD * 2),
                Math.Max(0, cell.Height - PAD * 2));

            var node = nodes[i];
            result.Add(new TreemapBlock
            {
                Node      = node,
                Bounds    = display,
                FillColor = ExtensionColors.Get(node.Extension, node.IsDirectory),
                Depth     = depth,
            });

            // 디렉터리면 자식도 레이아웃 (재귀)
            if (node.IsDirectory && depth < maxDepth && node.Children.Count > 0
                && display.Width > 4 && display.Height > 4)
            {
                Squarify(node.Children, display, node.Size, depth + 1, maxDepth, result);
            }
        }

        if (horiz)
            leftover = new Rect(bounds.X, bounds.Y + stripThick, bounds.Width, Math.Max(0, bounds.Height - stripThick));
        else
            leftover = new Rect(bounds.X + stripThick, bounds.Y, Math.Max(0, bounds.Width - stripThick), bounds.Height);
    }

    private static double WorstRatio(List<double> areas, int start, int count, double strip)
    {
        double rowArea = 0;
        for (int i = start; i < start + count; i++) rowArea += areas[i];
        if (rowArea == 0) return double.MaxValue;
        double thick = rowArea / strip;
        double worst = 0;
        for (int i = start; i < start + count; i++)
        {
            if (areas[i] == 0) continue;
            double len = areas[i] / thick;
            double r   = Math.Max(thick / len, len / thick);
            if (r > worst) worst = r;
        }
        return worst;
    }
}
