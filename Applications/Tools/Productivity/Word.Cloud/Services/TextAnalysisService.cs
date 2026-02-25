namespace WordCloud.Services;

public static class TextAnalysisService
{
    private static readonly HashSet<string> _koStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "은","는","이","가","을","를","의","에","에서","와","과","로","으로","도","만",
        "이다","이라","다","고","며","서","면","지","나","아","어","그","저","것","수",
        "등","및","즉","또","더","하다","있다","없다","되다","같다","위해","통해","대한",
        "따라","관한","때","중","후","전","하지","않다","한다","된다","이런","저런","어떤"
    };

    private static readonly HashSet<string> _enStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","and","or","but","in","on","at","to","for","of","with","by",
        "is","are","was","were","be","have","has","had","do","does","it","this","that",
        "as","not","from","we","you","he","she","they","i","me","my","our","their"
    };

    public static Dictionary<string, int> Analyze(
        string text,
        int maxWords,
        int minFreq,
        IEnumerable<string>? userStopWords = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var stopWords = new HashSet<string>(_koStopWords, StringComparer.OrdinalIgnoreCase);
        stopWords.UnionWith(_enStopWords);
        if (userStopWords != null)
            stopWords.UnionWith(userStopWords);

        var tokens = Regex.Split(text, @"[\s\p{P}\p{S}""''「」『』【】《》〈〉]+")
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .Select(t => t.Trim())
                          .Where(t => IsValidToken(t))
                          .Where(t => !stopWords.Contains(t))
                          .Select(t => t.ToLowerInvariant());

        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            freq.TryGetValue(token, out var cnt);
            freq[token] = cnt + 1;
        }

        return freq
            .Where(kv => kv.Value >= minFreq)
            .OrderByDescending(kv => kv.Value)
            .Take(maxWords)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static bool IsValidToken(string token)
    {
        if (token.Length < 2) return false;

        bool hasKorean = token.Any(c => c >= '\uAC00' && c <= '\uD7A3');
        if (hasKorean)
            return token.Length >= 2;

        bool hasEnglish = token.Any(char.IsLetter);
        if (hasEnglish)
            return token.Length >= 3;

        return false;
    }
}
