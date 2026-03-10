namespace Tag.Forge.Services;

using System.Text.RegularExpressions;

public static class FileNameParser
{
    static readonly string[] Tokens = ["track", "artist", "title", "album", "year", "genre"];

    /// <summary>파일명을 패턴으로 파싱해 태그 딕셔너리 반환. 실패 시 null.</summary>
    public static Dictionary<string, string>? Parse(string fileName, string pattern)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var regexStr = Regex.Escape(pattern);
        foreach (var t in Tokens)
            regexStr = regexStr.Replace($"\\{{{t}\\}}", $"(?<{t}>.+?)");
        regexStr = "^" + regexStr + "$";
        var m = Regex.Match(nameWithoutExt, regexStr, RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Tokens)
        {
            var g = m.Groups[t];
            if (g.Success) result[t] = g.Value.Trim();
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>태그 값을 패턴에 대입해 파일명 생성.</summary>
    public static string Build(string pattern, TrackInfo info)
    {
        var name = pattern
            .Replace("{track}",  info.Track.ToString("D2"))
            .Replace("{artist}", info.Artist)
            .Replace("{title}",  info.Title)
            .Replace("{album}",  info.Album)
            .Replace("{year}",   info.Year.ToString())
            .Replace("{genre}",  info.Genre);
        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch.ToString(), "");
        return name.Trim();
    }
}
