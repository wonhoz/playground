namespace DiskLens.Services;

/// <summary>Squarified Treemap 알고리즘 (Bruls et al.) — 폴더 헤더 + 무제한 깊이</summary>
public static class TreemapLayout
{
    private const int PAD         = 1;  // 셀 간 여백
    private const int FOLDER_HDR  = 16; // 폴더 헤더 높이 (레이블 영역)
    private const double MIN_AREA = 8;  // 재귀 중단 최소 크기 (픽셀)

    public static List<TreemapBlock> Build(FileNode root, Rect bounds)
    {
        var result = new List<TreemapBlock>();
        if (root.Size == 0 || bounds.Width < 2 || bounds.Height < 2) return result;
        Squarify(root.Children, bounds, 0, result);
        return result;
    }

    private static void Squarify(List<FileNode> nodes, Rect bounds, int depth, List<TreemapBlock> result)
    {
        if (nodes.Count == 0 || bounds.Width < MIN_AREA || bounds.Height < MIN_AREA) return;

        var sorted = nodes.Where(n => n.Size > 0).OrderByDescending(n => n.Size).ToList();
        if (sorted.Count == 0) return;

        // ★ 핵심 수정: parentSize 대신 sorted.Sum() 사용 → 화면을 항상 꽉 채움
        long totalSize = sorted.Sum(n => n.Size);
        double totalArea = bounds.Width * bounds.Height;
        double scale = totalArea / totalSize;
        var areas = sorted.Select(n => n.Size * scale).ToList();

        LayoutSquarified(sorted, areas, bounds, depth, result);
    }

    private static void LayoutSquarified(
        List<FileNode> nodes, List<double> areas, Rect bounds, int depth, List<TreemapBlock> result)
    {
        int idx = 0;
        var cur = bounds;

        while (idx < nodes.Count)
        {
            if (cur.Width < MIN_AREA || cur.Height < MIN_AREA) break;

            bool horiz = cur.Width >= cur.Height;
            double strip = horiz ? cur.Height : cur.Width;

            int best = 1;
            double bestWorst = WorstRatio(areas, idx, 1, strip);
            for (int k = 2; idx + k <= nodes.Count; k++)
            {
                double w = WorstRatio(areas, idx, k, strip);
                if (w <= bestWorst) { bestWorst = w; best = k; }
                else break;
            }

            PlaceRow(nodes, areas, idx, best, cur, horiz, strip, depth, result, out cur);
            idx += best;
        }
    }

    private static void PlaceRow(
        List<FileNode> nodes, List<double> areas,
        int start, int count, Rect bounds, bool horiz, double strip,
        int depth, List<TreemapBlock> result, out Rect leftover)
    {
        double rowArea = 0;
        for (int i = start; i < start + count; i++) rowArea += areas[i];
        double thick = rowArea / strip;

        double offset = 0;
        for (int i = start; i < start + count; i++)
        {
            double cellLen = areas[i] / thick;

            Rect cell = horiz
                ? new Rect(bounds.X + offset, bounds.Y, cellLen, thick)
                : new Rect(bounds.X, bounds.Y + offset, thick, cellLen);
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
                FillColor = node.IsDirectory
                    ? FolderColor(node.Name, depth)
                    : ExtensionColors.Get(node.Extension, false),
                Depth     = depth,
            });

            // ── 디렉터리 재귀 (깊이 제한 없음, 크기만 체크) ──
            if (node.IsDirectory && node.Children.Count > 0
                && display.Width > MIN_AREA && display.Height > MIN_AREA)
            {
                // 헤더 높이만큼 내부 영역 감소 → 폴더명 레이블 공간 확보
                bool hasHdr = display.Height > FOLDER_HDR + 6;
                var innerRect = hasHdr
                    ? new Rect(display.X, display.Y + FOLDER_HDR, display.Width, display.Height - FOLDER_HDR)
                    : display;
                Squarify(node.Children, innerRect, depth + 1, result);
            }
        }

        leftover = horiz
            ? new Rect(bounds.X, bounds.Y + thick, bounds.Width, Math.Max(0, bounds.Height - thick))
            : new Rect(bounds.X + thick, bounds.Y, Math.Max(0, bounds.Width - thick), bounds.Height);
    }

    // ── 폴더 색상: 이름 해시 → HSL 색상 (depth 별 밝기) ─────────────────────
    private static Color FolderColor(string name, int depth)
    {
        int hash = 5381;
        foreach (char c in name) hash = hash * 33 ^ c;
        int hue = Math.Abs(hash % 360);
        double lightness = Math.Max(0.18, 0.30 - depth * 0.04);
        return HslToRgb(hue, 0.40, lightness);
    }

    private static Color HslToRgb(int hue, double s, double l)
    {
        double h = hue / 360.0;
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        return Color.FromRgb(HueToRgb(p, q, h + 1.0/3), HueToRgb(p, q, h), HueToRgb(p, q, h - 1.0/3));
    }

    private static byte HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        double v = t < 1.0/6 ? p + (q-p)*6*t
                 : t < 0.5   ? q
                 : t < 2.0/3 ? p + (q-p)*(2.0/3-t)*6
                 : p;
        return (byte)(v * 255);
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
