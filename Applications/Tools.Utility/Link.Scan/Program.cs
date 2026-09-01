using System.Net;
using System.Text;

namespace LinkScan;

internal static class Program
{
    private const string Version = "1.0.0";

    private static string Reset => Ansi.Reset;
    private static string Bold => Ansi.Bold;
    private static string Dim => Ansi.Dim;
    private static string Cyan => Ansi.Cyan;
    private static string Green => Ansi.Green;
    private static string Red => Ansi.Red;
    private static string Yellow => Ansi.Yellow;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            WriteError(ex.Message);
            return 1;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        if (options.Target is null)
        {
            Console.Write($"{Cyan}대상 주소{Reset} > ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                WriteError("주소가 입력되지 않았습니다.");
                return 1;
            }

            try
            {
                options.SetTarget(input);
            }
            catch (ArgumentException ex)
            {
                WriteError(ex.Message);
                return 1;
            }
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var result = await ScanAsync(options, cts.Token);
            Render(result, options);
            await SaveAsync(result, options, cts.Token);
            return result.Links.Count > 0 ? 0 : 2;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            WriteError("사용자가 작업을 취소했습니다.");
            return 130;
        }
        catch (TaskCanceledException)
        {
            WriteError($"응답 시간이 초과되었습니다 ({options.TimeoutSeconds}초). --timeout 으로 늘릴 수 있습니다.");
            return 1;
        }
        catch (HttpRequestException ex)
        {
            var status = ex.StatusCode is { } code ? $" (HTTP {(int)code} {code})" : string.Empty;
            WriteError($"요청 실패{status}: {ex.Message}");
            WriteDetail(ex);
            return 1;
        }
        catch (IOException ex)
        {
            WriteError($"파일 저장 실패: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteError($"파일 접근 권한이 없습니다: {ex.Message}");
            return 1;
        }
    }

    private static async Task<ScanResult> ScanAsync(Options options, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All
        };

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

        var extractor = new LinkExtractor(httpClient);
        return await extractor.ScanAsync(options.Target!, options.Scope, options.MaxCount, cancellationToken);
    }

    private static void Render(ScanResult result, Options options)
    {
        if (options.Raw)
        {
            foreach (var link in result.Links)
                Console.WriteLine(link.Uri.AbsoluteUri);
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{Bold}{Cyan}Link.Scan{Reset} {Dim}v{Version}{Reset}");
        Console.WriteLine($"{Dim}--------------------------------------------------{Reset}");
        Console.WriteLine($"  대상  {result.Target.AbsoluteUri}");

        if (result.BaseUri != result.Target)
            Console.WriteLine($"  기준  {result.BaseUri.AbsoluteUri}{Dim}  (리다이렉트 또는 base 태그){Reset}");

        Console.WriteLine($"  범위  {ScopeLabel(options.Scope)}");
        Console.WriteLine($"  응답  {(int)result.StatusCode} {result.StatusCode} · {FormatSize(result.ByteLength)} · {result.Charset} · {result.Elapsed.TotalSeconds:0.00}s");
        Console.WriteLine();

        if (result.Links.Count == 0)
        {
            Console.WriteLine($"{Yellow}  범위에 해당하는 링크가 없습니다.{Reset}");
            Console.WriteLine($"{Dim}  페이지 전체 href {result.TotalHrefCount}개 · 중복 제거 {result.UniqueCount}개{Reset}");
            Console.WriteLine($"{Dim}  --scope host 또는 --scope all 로 범위를 넓혀 보세요.{Reset}");
            Console.WriteLine();
            return;
        }

        var width = result.Links.Count.ToString().Length;

        for (var i = 0; i < result.Links.Count; i++)
        {
            var link = result.Links[i];
            var index = (i + 1).ToString().PadLeft(width);
            Console.WriteLine($"{Dim}{index}.{Reset} {link.Uri.AbsoluteUri}");

            if (link.Text.Length > 0)
                Console.WriteLine($"{new string(' ', width + 2)}{Dim}{Truncate(link.Text, 90)}{Reset}");
        }

        Console.WriteLine();
        Console.WriteLine($"{Dim}--------------------------------------------------{Reset}");
        Console.WriteLine($"  {Green}{result.Links.Count}개{Reset} 추출 {Dim}· 페이지 전체 href {result.TotalHrefCount}개 · 중복 제거 후 {result.UniqueCount}개{Reset}");
        Console.WriteLine();
    }

    private static async Task SaveAsync(ScanResult result, Options options, CancellationToken cancellationToken)
    {
        if (options.OutPath is null)
            return;

        var fullPath = Path.GetFullPath(options.OutPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var lines = result.Links.Select(link => link.Uri.AbsoluteUri);
        await File.WriteAllLinesAsync(fullPath, lines, new UTF8Encoding(false), cancellationToken);

        if (!options.Raw)
            Console.WriteLine($"  {Green}저장 완료{Reset} {Dim}{fullPath}{Reset}\n");
    }

    private static string ScopeLabel(LinkScope scope) => scope switch
    {
        LinkScope.Sub => "하위 경로만 (sub)",
        LinkScope.Host => "같은 호스트 전체 (host)",
        _ => "외부 포함 전부 (all)"
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.00} MB"
    };

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");

    private static void WriteError(string message)
    {
        Console.Error.WriteLine($"{Red}오류{Reset} {message}");
        Console.Error.WriteLine($"{Dim}도움말: Link.Scan --help{Reset}");
    }

    /// <summary>
    /// HttpRequestException 은 실제 원인을 inner exception 에 담고 있어
    /// 겉면 메시지("SSL connection could not be established")만으로는 진단이 불가능하다.
    /// </summary>
    private static void WriteDetail(Exception exception)
    {
        var depth = 0;
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            depth++;
            Console.Error.WriteLine($"{Dim}  {new string(' ', depth * 2)}└ {inner.GetType().Name}: {inner.Message}{Reset}");
        }

        var hint = DiagnoseHint(exception);
        if (hint is not null)
            Console.Error.WriteLine($"{Yellow}  힌트{Reset} {hint}");
    }

    private static string? DiagnoseHint(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case System.Security.Authentication.AuthenticationException:
                    return "TLS 핸드셰이크 실패입니다. 서버 인증서가 신뢰되지 않거나(사내 프록시·자체 서명), "
                         + "서버가 요구하는 TLS 버전/암호군을 클라이언트가 지원하지 않는 경우입니다.";

                case System.Net.Sockets.SocketException socket:
                    return $"네트워크 계층에서 끊겼습니다 ({socket.SocketErrorCode}). "
                         + "DNS·방화벽·프록시 설정을 확인하세요.";
            }
        }

        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine($"""

            {Bold}{Cyan}Link.Scan{Reset} {Dim}v{Version}{Reset} — 페이지에 링크된 하위 페이지 주소를 추출한다.

            {Bold}사용법{Reset}
              Link.Scan <주소> [옵션]
              Link.Scan                     {Dim}주소를 대화형으로 입력{Reset}

            {Bold}옵션{Reset}
              --scope <sub|host|all>        추출 범위 (기본: sub)
                                              sub   대상 주소의 디렉토리 하위 경로만
                                              host  같은 호스트의 모든 경로
                                              all   외부 도메인 포함 전부
              -o, --out <파일>              결과를 텍스트 파일로 저장 (UTF-8, 한 줄에 하나)
              --max <개수>                  출력 개수 제한
              --timeout <초>                응답 대기 시간 (기본: 30)
              --user-agent <문자열>         User-Agent 헤더 지정
              --raw                         URL만 출력 (파이프 연결용)
              -h, --help                    이 도움말

            {Bold}예시{Reset}
              Link.Scan https://example.com/docs/
              Link.Scan example.com --scope host --out links.txt
              Link.Scan https://example.com --raw > links.txt

            {Bold}종료 코드{Reset}
              0 추출 성공  ·  1 오류  ·  2 링크 없음  ·  130 사용자 취소

            {Dim}※ 접근이 허용된 사이트에만 사용하세요. robots.txt / 이용약관 준수는 사용자 책임입니다.{Reset}

            """);
    }
}
