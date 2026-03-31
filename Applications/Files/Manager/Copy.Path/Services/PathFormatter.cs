namespace CopyPath.Services;

public record PathFormat(string Label, string Key, Func<string, string> Format);

public static class PathFormatter
{
    public static readonly PathFormat[] All =
    [
        new("전체 경로 (백슬래시)",     "full",     p => p),
        new("전체 경로 (슬래시)",       "slash",    p => p.Replace('\\', '/')),
        new("C# 문자열 리터럴",         "csharp",   p => p.Replace("\\", "\\\\")),
        new("파일명",                  "filename", p => Path.GetFileName(p)),
        new("확장자 없는 파일명",       "noext",    p => Path.GetFileNameWithoutExtension(p)),
        new("상위 폴더 경로",           "dir",      p => Path.GetDirectoryName(p) ?? p),
        new("file:/// URL",            "fileurl",  p => "file:///" + p.Replace('\\', '/').TrimStart('/')),
        new("Unix 스타일 (/c/...)",    "unix",     ToUnixStyle),
        new("UNC 경로 (\\\\서버\\...)", "unc",      ToUncPath),
    ];

    public static (string Label, string Key, string Value)[] FormatAll(string path)
        => All.Select(f =>
        {
            try   { return (f.Label, f.Key, f.Format(path)); }
            catch { return (f.Label, f.Key, string.Empty); }
        }).ToArray();

    private static string ToUnixStyle(string path)
    {
        if (path.Length >= 2 && path[1] == ':')
        {
            char drive = char.ToLower(path[0]);
            return "/" + drive + path[2..].Replace('\\', '/');
        }
        return path.Replace('\\', '/');
    }

    private static string ToUncPath(string path)
    {
        if (path.StartsWith(@"\\")) return path;
        if (path.Length >= 2 && path[1] == ':')
        {
            var host = Environment.MachineName;
            var share = path[0].ToString().ToUpper() + "$";
            return $@"\\{host}\{share}{path[2..]}";
        }
        return path;
    }
}
