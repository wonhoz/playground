namespace WordCloud.Services;

public static class TextAnalysisService
{
    private static readonly HashSet<string> _koStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // 조사
        "은","는","이","가","을","를","의","에","에서","와","과","로","으로","도","만",
        "에게","에서","부터","까지","라고","이라고","라는","이라는","처럼","만큼","보다",
        // 어미·접속
        "이다","이라","다","고","며","서","면","지","나","아","어","죠","요","네","죠",
        // 대명사·지시어
        "그","저","것","이것","저것","그것","여기","저기","거기","나","너","우리","저희",
        // 의존명사
        "수","등","및","즉","또","더","때","중","후","전","점","바","분","명","개",
        // 부사·접속부사
        "또한","특히","그리고","그러나","하지만","때문에","그래서","따라서","즉","물론",
        // 동사 어간 (형태소 분리된 경우)
        "하다","있다","없다","되다","같다","한다","된다","않다","이다","한다",
        // 형용사
        "이런","저런","어떤","이런","모든","각","각각",
        // 기타
        "위해","통해","대한","따라","관한","있는","없는","되는","하는","된","한","위",
        "관련","통한","이후","이전","이상","이하","여러","다양","함께","서로"
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
