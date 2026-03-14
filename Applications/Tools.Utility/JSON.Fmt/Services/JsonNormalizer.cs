using System.Text;
using System.Text.RegularExpressions;

namespace JsonFmt.Services;

/// <summary>
/// Lenient JSON 전처리기:
/// - // 한 줄 주석 제거
/// - /* */ 블록 주석 제거
/// - trailing comma (,]) (,}) 제거
/// - 단일 따옴표 → 이중 따옴표 변환
/// - 키 무따옴표 → 따옴표 추가
/// </summary>
public static class JsonNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var result = StripComments(input);
        result = FixSingleQuotes(result);
        result = FixUnquotedKeys(result);
        result = RemoveTrailingCommas(result);
        return result;
    }

    // 문자열 리터럴 내부를 건드리지 않고 주석만 제거
    private static string StripComments(string input)
    {
        var sb = new StringBuilder(input.Length);
        int i = 0;
        while (i < input.Length)
        {
            // 이중 따옴표 문자열 — 그대로 복사
            if (input[i] == '"')
            {
                sb.Append(input[i++]);
                while (i < input.Length)
                {
                    char c = input[i++];
                    sb.Append(c);
                    if (c == '\\' && i < input.Length) { sb.Append(input[i++]); }
                    else if (c == '"') break;
                }
                continue;
            }

            // 단일 따옴표 문자열 — 그대로 복사 (단일→이중 변환은 별도 단계)
            if (input[i] == '\'')
            {
                sb.Append(input[i++]);
                while (i < input.Length)
                {
                    char c = input[i++];
                    sb.Append(c);
                    if (c == '\\' && i < input.Length) { sb.Append(input[i++]); }
                    else if (c == '\'') break;
                }
                continue;
            }

            // 블록 주석 /* ... */
            if (i + 1 < input.Length && input[i] == '/' && input[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/'))
                    i++;
                i += 2; // skip */
                continue;
            }

            // 한 줄 주석 //
            if (i + 1 < input.Length && input[i] == '/' && input[i + 1] == '/')
            {
                i += 2;
                while (i < input.Length && input[i] != '\n')
                    i++;
                continue;
            }

            sb.Append(input[i++]);
        }
        return sb.ToString();
    }

    // 단일 따옴표 문자열 → 이중 따옴표 변환
    private static string FixSingleQuotes(string input)
    {
        var sb = new StringBuilder(input.Length);
        int i = 0;
        while (i < input.Length)
        {
            // 이중 따옴표 문자열 — 건드리지 않음
            if (input[i] == '"')
            {
                sb.Append(input[i++]);
                while (i < input.Length)
                {
                    char c = input[i++];
                    sb.Append(c);
                    if (c == '\\' && i < input.Length) { sb.Append(input[i++]); }
                    else if (c == '"') break;
                }
                continue;
            }

            // 단일 따옴표 → 이중 따옴표로 변환
            if (input[i] == '\'')
            {
                sb.Append('"');
                i++;
                while (i < input.Length)
                {
                    char c = input[i++];
                    if (c == '\\' && i < input.Length)
                    {
                        char next = input[i++];
                        if (next == '\'') sb.Append('\''); // \' → '
                        else { sb.Append('\\'); sb.Append(next); }
                    }
                    else if (c == '"') { sb.Append('\\'); sb.Append('"'); } // " inside → \"
                    else if (c == '\'') { sb.Append('"'); break; }
                    else sb.Append(c);
                }
                continue;
            }

            sb.Append(input[i++]);
        }
        return sb.ToString();
    }

    // 무따옴표 키 → 따옴표 추가  예: { foo: "bar" } → { "foo": "bar" }
    private static string FixUnquotedKeys(string input)
    {
        // 객체 내 키 패턴: { 또는 , 다음에 공백 후 식별자:
        return Regex.Replace(input, @"(?<=[{,]\s*)([A-Za-z_$][A-Za-z0-9_$]*)\s*:", m =>
        {
            return $"\"{m.Groups[1].Value}\":";
        });
    }

    // trailing comma: ,] 또는 ,} 패턴 제거
    private static string RemoveTrailingCommas(string input)
    {
        // 반복해서 제거 (중첩된 경우 대비)
        string prev;
        do
        {
            prev = input;
            input = Regex.Replace(input, @",(\s*[\]\}])", "$1");
        } while (input != prev);
        return input;
    }
}
