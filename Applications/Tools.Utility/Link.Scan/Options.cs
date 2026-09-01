namespace LinkScan;

/// <summary>추출 범위 — 어떤 링크를 결과에 포함할지 결정한다.</summary>
internal enum LinkScope
{
    /// <summary>대상 주소의 디렉토리 하위 경로만 (기본값)</summary>
    Sub,

    /// <summary>같은 호스트의 모든 경로</summary>
    Host,

    /// <summary>외부 도메인 포함 전부</summary>
    All
}

internal sealed class Options
{
    public Uri? Target { get; private set; }
    public LinkScope Scope { get; private set; } = LinkScope.Sub;
    public string? OutPath { get; private set; }
    public int TimeoutSeconds { get; private set; } = 30;
    public string UserAgent { get; private set; } = "Link.Scan/1.0 (+https://github.com/wonhoz/playground)";
    public int MaxCount { get; private set; } = int.MaxValue;
    public bool Raw { get; private set; }
    public bool ShowHelp { get; private set; }

    public static Options Parse(string[] args)
    {
        var options = new Options();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h" or "--help":
                    options.ShowHelp = true;
                    return options;

                case "--scope":
                    options.Scope = ParseScope(Next(args, ref i, arg));
                    break;

                case "--out" or "-o":
                    options.OutPath = Next(args, ref i, arg);
                    break;

                case "--timeout":
                    options.TimeoutSeconds = ParsePositive(Next(args, ref i, arg), arg);
                    break;

                case "--user-agent":
                    options.UserAgent = Next(args, ref i, arg);
                    break;

                case "--max":
                    options.MaxCount = ParsePositive(Next(args, ref i, arg), arg);
                    break;

                case "--raw":
                    options.Raw = true;
                    break;

                default:
                    if (arg.StartsWith('-'))
                        throw new ArgumentException($"알 수 없는 옵션입니다: {arg}");
                    if (options.Target is not null)
                        throw new ArgumentException($"대상 주소는 하나만 지정할 수 있습니다: {arg}");
                    options.Target = ParseUrl(arg);
                    break;
            }
        }

        return options;
    }

    /// <summary>대화형 입력 등으로 뒤늦게 얻은 주소를 채운다.</summary>
    public void SetTarget(string url) => Target = ParseUrl(url);

    /// <summary>스킴이 생략된 입력은 https 로 보정한다.</summary>
    public static Uri ParseUrl(string value)
    {
        var text = value.Trim().Trim('"', '\'');
        if (text.Length == 0)
            throw new ArgumentException("주소가 비어 있습니다.");

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            throw new ArgumentException($"주소 형식이 올바르지 않습니다: {value}");

        if (uri.Scheme is not ("http" or "https"))
            throw new ArgumentException($"http/https 주소만 지원합니다: {uri.Scheme}");

        return uri;
    }

    private static string Next(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{option} 옵션에 값이 필요합니다.");
        return args[++index];
    }

    private static LinkScope ParseScope(string value) => value.ToLowerInvariant() switch
    {
        "sub" => LinkScope.Sub,
        "host" => LinkScope.Host,
        "all" => LinkScope.All,
        _ => throw new ArgumentException($"--scope 값은 sub / host / all 중 하나여야 합니다: {value}")
    };

    private static int ParsePositive(string value, string option)
    {
        if (!int.TryParse(value, out var number) || number <= 0)
            throw new ArgumentException($"{option} 값은 1 이상의 정수여야 합니다: {value}");
        return number;
    }
}
