namespace ClothCut.Entities;

/// <summary>
/// 스프링-질량 천 격자.
/// 노드 = Rows × Cols 질점, 링크 = Structural(수평/수직) + Shear(대각선).
/// </summary>
public class ClothMesh
{
    public int         Cols, Rows;
    public double      NodeSpacing;
    public double      Stiffness;   // 0.4(탄성) ~ 1.0(뻣뻣)
    public ClothNode[] Nodes  = null!;
    public List<ClothLink> Links = [];

    // 링크 2D 인덱스 (빠른 접근용)
    // HorzLinks[r, c]: 노드(r,c)↔(r,c+1)
    // VertLinks[r, c]: 노드(r,c)↔(r+1,c)
    public ClothLink?[,] HorzLinks = null!;  // [Rows, Cols-1]
    public ClothLink?[,] VertLinks = null!;  // [Rows-1, Cols]

    public int NodeIndex(int row, int col) => row * Cols + col;
    public ClothNode NodeAt(int row, int col) => Nodes[NodeIndex(row, col)];

    /// <summary>격자 생성 팩토리.</summary>
    public static ClothMesh Create(
        int cols, int rows, double spacing,
        double startX, double startY,
        IEnumerable<int> pinnedCols,
        double stiffness)
    {
        var mesh = new ClothMesh
        {
            Cols = cols, Rows = rows,
            NodeSpacing = spacing,
            Stiffness   = stiffness,
            Nodes       = new ClothNode[cols * rows],
            HorzLinks   = new ClothLink?[rows, cols - 1],
            VertLinks   = new ClothLink?[rows - 1, cols]
        };

        var pinSet = new HashSet<int>(pinnedCols);

        // 노드 생성
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var n = new ClothNode
            {
                X        = startX + c * spacing,
                Y        = startY + r * spacing,
                IsPinned = r == 0 && pinSet.Contains(c),
                Row = r, Col = c
            };
            n.InitOld();
            mesh.Nodes[mesh.NodeIndex(r, c)] = n;
        }

        // Structural 링크 — 수평
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            var link = new ClothLink(mesh.NodeAt(r, c), mesh.NodeAt(r, c + 1), LinkType.Structural);
            mesh.HorzLinks[r, c] = link;
            mesh.Links.Add(link);
        }

        // Structural 링크 — 수직
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols; c++)
        {
            var link = new ClothLink(mesh.NodeAt(r, c), mesh.NodeAt(r + 1, c), LinkType.Structural);
            mesh.VertLinks[r, c] = link;
            mesh.Links.Add(link);
        }

        // Shear 링크 — 대각선 (좌상↘ 및 우상↙)
        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            mesh.Links.Add(new ClothLink(mesh.NodeAt(r, c),     mesh.NodeAt(r + 1, c + 1), LinkType.Shear));
            mesh.Links.Add(new ClothLink(mesh.NodeAt(r, c + 1), mesh.NodeAt(r + 1, c),     LinkType.Shear));
        }

        return mesh;
    }

    // ── 컴포넌트 분리 (BFS) ───────────────────────────────

    /// <summary>
    /// 절단 후 연결 컴포넌트를 재계산.
    /// 반환값: 컴포넌트별 노드 목록 리스트.
    /// </summary>
    public List<List<ClothNode>> RecalculateComponents()
    {
        // 인접 목록 구성 (끊어지지 않은 링크만)
        var adj = new Dictionary<ClothNode, List<ClothNode>>();
        foreach (var n in Nodes)
            adj[n] = [];

        foreach (var link in Links)
        {
            if (link.IsCut) continue;
            adj[link.A].Add(link.B);
            adj[link.B].Add(link.A);
        }

        // BFS
        var visited    = new HashSet<ClothNode>();
        var components = new List<List<ClothNode>>();

        foreach (var start in Nodes)
        {
            if (visited.Contains(start)) continue;

            var comp  = new List<ClothNode>();
            var queue = new Queue<ClothNode>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                comp.Add(cur);
                foreach (var nb in adj[cur])
                {
                    if (!visited.Contains(nb))
                    {
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
            }

            components.Add(comp);
        }

        return components;
    }

    // ── 셀 링크 참조 헬퍼 ────────────────────────────────

    /// <summary>셀(r, c)의 4개 경계 링크 중 하나라도 절단됐는지.</summary>
    public bool IsCellBroken(int row, int col)
    {
        var h1 = HorzLinks[row,     col];       // 상단
        var h2 = HorzLinks[row + 1, col];       // 하단
        var v1 = VertLinks[row,     col];       // 좌측
        var v2 = VertLinks[row,     col + 1];   // 우측

        return (h1?.IsCut ?? true)
            || (h2?.IsCut ?? true)
            || (v1?.IsCut ?? true)
            || (v2?.IsCut ?? true);
    }
}
