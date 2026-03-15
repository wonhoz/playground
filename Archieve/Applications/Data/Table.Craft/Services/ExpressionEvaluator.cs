using System.Text.RegularExpressions;

namespace TableCraft.Services;

/// <summary>계산 컬럼용 간단 수식 평가기
/// 지원: 사칙연산(+,-,*,/), 문자열 연결(&amp;), IF(), UPPER(), LOWER(), LEN(), TRIM(), 컬럼 참조([컬럼명] 또는 A~Z)
/// </summary>
public static class ExpressionEvaluator
{
    public static string Evaluate(string expr, string[] headers, string[] row)
    {
        expr = expr.Trim();
        if (expr.StartsWith('=')) expr = expr[1..].TrimStart();

        // 컬럼 참조 치환: [컬럼명] → 값
        expr = Regex.Replace(expr, @"\[([^\]]+)\]", m =>
        {
            var name = m.Groups[1].Value;
            var idx  = Array.FindIndex(headers, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? Quote(row.ElementAtOrDefault(idx) ?? "") : "\"\"";
        });

        // 단일 문자 컬럼 참조: A, B, C ... (대소문자)
        expr = Regex.Replace(expr, @"\b([A-Z])\b", m =>
        {
            int idx = m.Groups[1].Value[0] - 'A';
            return idx < row.Length ? Quote(row[idx]) : "\"\"";
        });

        return EvalNode(expr);
    }

    // ── 단순 재귀 평가 ────────────────────────────────────────────────
    private static string EvalNode(string expr)
    {
        expr = expr.Trim();

        // 함수 호출
        var funcMatch = Regex.Match(expr, @"^(\w+)\((.+)\)$", RegexOptions.Singleline);
        if (funcMatch.Success)
        {
            var fn   = funcMatch.Groups[1].Value.ToUpperInvariant();
            var args = SplitArgs(funcMatch.Groups[2].Value);
            return fn switch
            {
                "IF"    => args.Length >= 3 ? EvalIf(args[0], args[1], args[2])   : "#ARG",
                "UPPER" => args.Length >= 1 ? EvalNode(args[0]).ToUpperInvariant() : "#ARG",
                "LOWER" => args.Length >= 1 ? EvalNode(args[0]).ToLowerInvariant() : "#ARG",
                "LEN"   => args.Length >= 1 ? EvalNode(args[0]).Length.ToString()  : "#ARG",
                "TRIM"  => args.Length >= 1 ? EvalNode(args[0]).Trim()             : "#ARG",
                "LEFT"  => args.Length >= 2 ? EvalNode(args[0])[..Math.Min(
                               int.TryParse(EvalNode(args[1]), out var n) ? n : 0,
                               EvalNode(args[0]).Length)]                           : "#ARG",
                _       => "#FUNC"
            };
        }

        // 문자열 리터럴
        if (expr.StartsWith('"') && expr.EndsWith('"') && expr.Length >= 2)
            return expr[1..^1].Replace("\"\"", "\"");

        // 숫자 리터럴
        if (double.TryParse(expr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numLit))
            return expr;

        // 이항 연산자 (우선순위: + - 낮음, * / 높음, & 가장 낮음)
        // 간단 구현: 마지막 연산자부터 왼쪽으로 찾기 (괄호 고려)
        int depth = 0;
        for (int i = expr.Length - 1; i >= 0; i--)
        {
            char ch = expr[i];
            if (ch == ')') depth++;
            else if (ch == '(') depth--;
            if (depth != 0) continue;

            if (ch == '&')
                return EvalNode(expr[..i]) + EvalNode(expr[(i + 1)..]);
        }

        for (int i = expr.Length - 1; i >= 0; i--)
        {
            char ch = expr[i];
            if (ch == ')') depth++;
            else if (ch == '(') depth--;
            if (depth != 0) continue;

            if ((ch == '+' || ch == '-') && i > 0)
            {
                var left  = expr[..i].TrimEnd();
                var right = expr[(i + 1)..].TrimStart();
                if (TryNum(EvalNode(left), out var l) && TryNum(EvalNode(right), out var r))
                    return ch == '+' ? FmtNum(l + r) : FmtNum(l - r);
                if (ch == '+') return EvalNode(left) + EvalNode(right);
            }
        }

        for (int i = expr.Length - 1; i >= 0; i--)
        {
            char ch = expr[i];
            if (ch == ')') depth++;
            else if (ch == '(') depth--;
            if (depth != 0) continue;

            if ((ch == '*' || ch == '/') && i > 0)
            {
                if (TryNum(EvalNode(expr[..i]), out var l) && TryNum(EvalNode(expr[(i + 1)..]), out var r))
                {
                    if (ch == '/') return r == 0 ? "#DIV0" : FmtNum(l / r);
                    return FmtNum(l * r);
                }
            }
        }

        // 괄호 벗기기
        if (expr.StartsWith('(') && expr.EndsWith(')'))
            return EvalNode(expr[1..^1]);

        return expr;  // 그대로 반환
    }

    private static string EvalIf(string cond, string then, string els)
    {
        var c = EvalNode(cond).Trim().ToLowerInvariant();
        bool truthy = c is "true" or "1" or "yes" or "y"
                   || (double.TryParse(c, out var d) && d != 0)
                   || (c != "false" && c != "0" && c != "" && c != "no" && c != "n");
        return EvalNode(truthy ? then : els);
    }

    private static string[] SplitArgs(string s)
    {
        var args  = new List<string>();
        int depth = 0, start = 0;
        bool inStr = false;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '"') inStr = !inStr;
            if (inStr) continue;
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0)
            {
                args.Add(s[start..i].Trim());
                start = i + 1;
            }
        }
        args.Add(s[start..].Trim());
        return args.ToArray();
    }

    private static bool TryNum(string s, out double v)
        => double.TryParse(s.Trim(), System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture, out v);

    private static string FmtNum(double d)
        => d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString() : d.ToString("G10");

    private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
}
