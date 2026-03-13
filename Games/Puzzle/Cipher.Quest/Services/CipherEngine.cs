namespace CipherQuest.Services;

public static class CipherEngine
{
    // ── Caesar ───────────────────────────────────────────────────────

    public static string CaesarEncrypt(string text, int shift) =>
        Shift(text.ToUpper(), shift);

    public static string CaesarDecrypt(string text, int shift) =>
        Shift(text.ToUpper(), -shift);

    private static string Shift(string text, int shift) =>
        new(text.Select(c =>
            char.IsLetter(c) ? (char)(((c - 'A' + shift) % 26 + 26) % 26 + 'A') : c).ToArray());

    // ── Vigenere ─────────────────────────────────────────────────────

    public static string VigenereEncrypt(string text, string key) => Vigenere(text, key, false);
    public static string VigenereDecrypt(string text, string key) => Vigenere(text, key, true);

    private static string Vigenere(string text, string key, bool decrypt)
    {
        if (string.IsNullOrWhiteSpace(key)) return text;
        key = key.ToUpper().Where(char.IsLetter).ToArray() is { Length: > 0 } k ? new string(k) : "A";
        int ki = 0;
        return new string(text.ToUpper().Select(c =>
        {
            if (!char.IsLetter(c)) return c;
            int s = key[ki++ % key.Length] - 'A';
            return (char)(((c - 'A') + (decrypt ? -s : s) + 26) % 26 + 'A');
        }).ToArray());
    }

    // ── Substitution ─────────────────────────────────────────────────

    /// <summary>key[i] = cipher char for plain 'A'+i</summary>
    public static string SubstitutionEncrypt(string text, string key) =>
        new(text.ToUpper().Select(c => char.IsLetter(c) ? key[c - 'A'] : c).ToArray());

    /// <summary>mapping[i] = plain char that cipher 'A'+i decodes to ('\0' = unknown)</summary>
    public static string SubstitutionDecrypt(string text, char[] mapping) =>
        new(text.ToUpper().Select(c =>
            char.IsLetter(c) ? (mapping[c - 'A'] != '\0' ? mapping[c - 'A'] : '_') : c).ToArray());

    // ── Rail Fence ───────────────────────────────────────────────────

    public static string RailFenceEncrypt(string text, int rails)
    {
        if (rails <= 1 || rails >= text.Length) return text;
        var fence = new List<char>[rails];
        for (int i = 0; i < rails; i++) fence[i] = [];
        var pat = RailPattern(text.Length, rails);
        for (int i = 0; i < text.Length; i++) fence[pat[i]].Add(text[i]);
        return new string(fence.SelectMany(r => r).ToArray());
    }

    public static string RailFenceDecrypt(string text, int rails)
    {
        if (rails <= 1 || rails >= text.Length) return text;
        var pat = RailPattern(text.Length, rails);
        var result = new char[text.Length];
        int pos = 0;
        for (int r = 0; r < rails; r++)
            for (int i = 0; i < text.Length; i++)
                if (pat[i] == r) result[i] = text[pos++];
        return new string(result);
    }

    private static int[] RailPattern(int len, int rails)
    {
        var arr = new int[len];
        int r = 0, d = 1;
        for (int i = 0; i < len; i++)
        {
            arr[i] = r;
            if (r == 0) d = 1;
            else if (r == rails - 1) d = -1;
            r += d;
        }
        return arr;
    }

    // ── Enigma (simplified symmetric cipher) ─────────────────────────

    public static string EnigmaEncrypt(string text, char r1, char r2, char r3) =>
        EnigmaProcess(text, r1, r2, r3);

    public static string EnigmaDecrypt(string text, char r1, char r2, char r3) =>
        EnigmaProcess(text, r1, r2, r3); // symmetric

    private static string EnigmaProcess(string text, char r1, char r2, char r3)
    {
        int c1 = r1 - 'A', c2 = r2 - 'A', c3 = r3 - 'A';
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text.ToUpper())
        {
            if (!char.IsLetter(ch)) { sb.Append(ch); continue; }
            int v = ch - 'A';
            v = (v + c3) % 26;
            v = (v + c2) % 26;
            v = (v + c1) % 26;
            v = 25 - v;               // reflector
            v = (v - c1 + 26) % 26;
            v = (v - c2 + 26) % 26;
            v = (v - c3 + 26) % 26;
            sb.Append((char)('A' + v));
            // rotor stepping
            c3 = (c3 + 1) % 26;
            if (c3 == 0) { c2 = (c2 + 1) % 26; if (c2 == 0) c1 = (c1 + 1) % 26; }
        }
        return sb.ToString();
    }

    // ── Frequency Analysis ────────────────────────────────────────────

    public static double[] LetterFrequency(string text)
    {
        var freq = new double[26];
        int total = 0;
        foreach (char c in text.ToUpper().Where(char.IsLetter)) { freq[c - 'A']++; total++; }
        if (total > 0) for (int i = 0; i < 26; i++) freq[i] = freq[i] / total * 100.0;
        return freq;
    }

    public static readonly double[] EnglishFreq =
    [
        8.2, 1.5, 2.8, 4.3, 12.7, 2.2, 2.0, 6.1, 7.0, 0.2, 0.8, 4.0, 2.4,
        6.7, 7.5, 1.9, 0.1, 6.0, 6.3, 9.1, 2.8, 1.0, 2.4, 0.2, 2.0, 0.1
    ];
}
