using DeepDiff.Models;

namespace DeepDiff.Services;

public class TextDiffService
{
    public (List<AlignedDiffLine> lines, int diffCount) Compare(
        string leftPath, string rightPath, bool ignoreWhitespace = false)
    {
        var leftLines  = ReadLines(leftPath);
        var rightLines = ReadLines(rightPath);
        return BuildAligned(leftLines, rightLines, ignoreWhitespace);
    }

    public (List<AlignedDiffLine> lines, int diffCount) CompareText(
        string leftText, string rightText, bool ignoreWhitespace = false)
    {
        var leftLines  = leftText.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var rightLines = rightText.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        return BuildAligned(leftLines, rightLines, ignoreWhitespace);
    }

    private static List<string> ReadLines(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path).ToList();
    }

    private static (List<AlignedDiffLine>, int) BuildAligned(
        List<string> left, List<string> right, bool ignoreWS)
    {
        var lComp = ignoreWS ? left.Select(l => l.Trim()).ToList() : left;
        var rComp = ignoreWS ? right.Select(l => l.Trim()).ToList() : right;

        var ops = DiffAlgorithm.Diff(lComp, rComp);

        var result = new List<AlignedDiffLine>();
        int diffCount = 0;

        int li = 1, ri = 1;
        int i = 0;
        while (i < ops.Count)
        {
            var op = ops[i];

            if (op.Type == DiffAlgorithm.EditType.Equal)
            {
                result.Add(new AlignedDiffLine
                {
                    LeftLineNum  = li++,
                    RightLineNum = ri++,
                    Status       = LineStatus.Same,
                    LeftSegments  = [new(left[op.LeftIndex], false)],
                    RightSegments = [new(right[op.RightIndex], false)]
                });
                i++;
            }
            else
            {
                // 연속된 Delete / Insert를 묶어서 Replace로 처리
                var delOps = new List<DiffAlgorithm.EditOp>();
                var insOps = new List<DiffAlgorithm.EditOp>();

                while (i < ops.Count && ops[i].Type == DiffAlgorithm.EditType.Delete)
                    delOps.Add(ops[i++]);
                while (i < ops.Count && ops[i].Type == DiffAlgorithm.EditType.Insert)
                    insOps.Add(ops[i++]);

                int maxPairs = Math.Max(delOps.Count, insOps.Count);
                for (int p = 0; p < maxPairs; p++)
                {
                    diffCount++;
                    bool hasDel = p < delOps.Count;
                    bool hasIns = p < insOps.Count;

                    if (hasDel && hasIns)
                    {
                        // Changed line — 문자 수준 diff
                        var leftLine  = left[delOps[p].LeftIndex];
                        var rightLine = right[insOps[p].RightIndex];
                        result.Add(new AlignedDiffLine
                        {
                            LeftLineNum  = li++,
                            RightLineNum = ri++,
                            Status       = LineStatus.Changed,
                            LeftSegments  = BuildCharDiff(leftLine, rightLine, true),
                            RightSegments = BuildCharDiff(leftLine, rightLine, false)
                        });
                    }
                    else if (hasDel)
                    {
                        result.Add(new AlignedDiffLine
                        {
                            LeftLineNum   = li++,
                            RightLineNum  = null,
                            Status        = LineStatus.LeftOnly,
                            LeftSegments  = [new(left[delOps[p].LeftIndex], false)],
                            RightSegments = []
                        });
                    }
                    else
                    {
                        result.Add(new AlignedDiffLine
                        {
                            LeftLineNum   = null,
                            RightLineNum  = ri++,
                            Status        = LineStatus.RightOnly,
                            LeftSegments  = [],
                            RightSegments = [new(right[insOps[p].RightIndex], false)]
                        });
                    }
                }
            }
        }

        return (result, diffCount);
    }

    private static List<TextSegment> BuildCharDiff(string left, string right, bool useLeft)
    {
        var charOps = DiffAlgorithm.DiffChars(left, right);
        var segs = new List<TextSegment>();

        foreach (var op in charOps)
        {
            bool isHL;
            string ch;
            if (useLeft)
            {
                if (op.Type == DiffAlgorithm.EditType.Insert) continue;
                ch = op.LeftIndex >= 0 ? left[op.LeftIndex].ToString() : "";
                isHL = op.Type == DiffAlgorithm.EditType.Delete;
            }
            else
            {
                if (op.Type == DiffAlgorithm.EditType.Delete) continue;
                ch = op.RightIndex >= 0 ? right[op.RightIndex].ToString() : "";
                isHL = op.Type == DiffAlgorithm.EditType.Insert;
            }

            if (segs.Count > 0 && segs[^1].IsHighlighted == isHL)
                segs[^1] = segs[^1] with { Text = segs[^1].Text + ch };
            else
                segs.Add(new(ch, isHL));
        }

        return segs;
    }
}
