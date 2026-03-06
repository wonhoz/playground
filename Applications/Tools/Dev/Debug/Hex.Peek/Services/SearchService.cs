namespace HexPeek.Services;

public enum SearchMode { Hex, Ascii, Regex }

public static class SearchService
{
    // ── 전방/후방 검색 ────────────────────────────────────────────────────
    public static long Search(HexDocument doc, byte[] pattern, long startOffset, bool forward)
    {
        if (pattern.Length == 0 || doc.Length == 0) return -1;

        long maxStart = doc.Length - pattern.Length;
        if (maxStart < 0) return -1;

        startOffset = Math.Clamp(startOffset, 0, maxStart);

        if (forward)
        {
            for (long i = startOffset; i <= maxStart; i++)
                if (MatchAt(doc, pattern, i)) return i;
        }
        else
        {
            for (long i = Math.Min(startOffset, maxStart); i >= 0; i--)
                if (MatchAt(doc, pattern, i)) return i;
        }

        return -1;
    }

    // ── 정규식 검색 (ASCII 범위) ──────────────────────────────────────────
    public static long SearchRegex(HexDocument doc, string pattern, long startOffset, bool forward)
    {
        try
        {
            var re    = new Regex(pattern, RegexOptions.Compiled);
            long len  = Math.Min(doc.Length, 10 * 1024 * 1024); // 최대 10MB만 텍스트로 변환
            var bytes = new byte[len];
            doc.ReadBytes(0, bytes, (int)len);
            var text  = Encoding.Latin1.GetString(bytes);

            if (forward)
            {
                var m = re.Match(text, (int)Math.Min(startOffset, text.Length - 1));
                return m.Success ? m.Index : -1;
            }
            else
            {
                var matches = re.Matches(text[..(int)Math.Min(startOffset + 1, text.Length)]);
                return matches.Count > 0 ? matches[^1].Index : -1;
            }
        }
        catch { return -1; }
    }

    // ── HEX 문자열 파싱 ───────────────────────────────────────────────────
    public static byte[]? ParseHex(string hexStr)
    {
        hexStr = hexStr.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "");
        if (hexStr.Length == 0 || hexStr.Length % 2 != 0) return null;

        try
        {
            var bytes = new byte[hexStr.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hexStr.Substring(i * 2, 2), 16);
            return bytes;
        }
        catch { return null; }
    }

    // ── 내부 ──────────────────────────────────────────────────────────────
    private static bool MatchAt(HexDocument doc, byte[] pattern, long offset)
    {
        for (int j = 0; j < pattern.Length; j++)
            if (doc.ReadByte(offset + j) != pattern[j]) return false;
        return true;
    }
}
