namespace LeafGrow.Services;

/// <summary>L-시스템 전개 + 터틀 그래픽 렌더링</summary>
public static class LSystemEngine
{
    // ── 문자열 전개 ───────────────────────────────────────────────
    public static string Expand(string axiom, List<LRule> rules, int iterations)
    {
        var current = axiom;
        for (int i = 0; i < iterations; i++)
        {
            var sb = new System.Text.StringBuilder(current.Length * 3);
            foreach (char c in current)
            {
                var rule = rules.FirstOrDefault(r => r.From == c);
                sb.Append(rule != null ? rule.To : c.ToString());
            }
            current = sb.ToString();
            if (current.Length > 200_000) break; // 메모리 방어
        }
        return current;
    }

    // ── 터틀 그래픽 → Segment 목록 ───────────────────────────────
    public static List<Segment> Render(
        PlantSpecies species,
        string lstring,
        double originX,
        double originY,
        double baseLength,
        double growthRate = 1.0)
    {
        var segments = new List<Segment>();
        var stack    = new Stack<TurtleState>();

        double angleRad = species.Angle * Math.PI / 180.0;
        double x = originX, y = originY;
        double dir = -Math.PI / 2; // 위쪽 방향
        int depth = 0;

        // 최대 깊이 계산 (두께/색상용)
        int maxDepth = CountMaxDepth(lstring);

        foreach (char c in lstring)
        {
            switch (c)
            {
                case 'F':
                case 'G':
                {
                    double len = baseLength
                        * Math.Pow(species.LenDecay, depth)
                        * (0.8 + growthRate * 0.2);

                    double nx = x + Math.Cos(dir) * len;
                    double ny = y + Math.Sin(dir) * len;

                    // 가지 굵기 (깊이가 얕을수록 굵게)
                    double thickness = Math.Max(0.5,
                        (1.0 - (double)depth / Math.Max(1, maxDepth)) * 3.5 + 0.5);

                    // 잎 여부: 스택에 아무것도 없거나 깊이가 깊으면 잎 색상
                    bool isLeaf = depth >= maxDepth - 1;
                    var color   = isLeaf ? species.LeafColor : species.TrunkColor;

                    segments.Add(new Segment
                    {
                        X1 = x, Y1 = y, X2 = nx, Y2 = ny,
                        Thickness = thickness,
                        Color     = color,
                        Type      = isLeaf ? SegType.Leaf : SegType.Branch,
                        Depth     = depth,
                    });

                    x = nx; y = ny;
                    break;
                }
                case '+': dir += angleRad;  break;
                case '-': dir -= angleRad;  break;
                case '[':
                    stack.Push(new TurtleState(x, y, dir, depth));
                    depth++;
                    break;
                case ']':
                    if (stack.Count > 0)
                    {
                        // 꽃 또는 잎 점 추가 (깊이가 깊은 끝에서)
                        if (species.HasFlower && depth >= maxDepth - 1 && growthRate >= 0.8)
                        {
                            segments.Add(new Segment
                            {
                                X1 = x, Y1 = y, X2 = x, Y2 = y,
                                Thickness = 5,
                                Color     = species.FlowerColor,
                                Type      = SegType.Flower,
                                Depth     = depth,
                            });
                        }
                        var s = stack.Pop();
                        x = s.X; y = s.Y; dir = s.Dir; depth = s.Depth;
                    }
                    break;
            }
        }

        return segments;
    }

    private static int CountMaxDepth(string lstring)
    {
        int d = 0, max = 0;
        foreach (char c in lstring)
        {
            if (c == '[') { d++; if (d > max) max = d; }
            else if (c == ']') d--;
        }
        return Math.Max(1, max);
    }

    private record struct TurtleState(double X, double Y, double Dir, int Depth);
}
