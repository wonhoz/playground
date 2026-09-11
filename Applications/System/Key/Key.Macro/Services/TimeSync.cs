using System.Diagnostics;
using System.Net.Http;

namespace KeyMacro.Services;

/// <summary>시간 확인 결과</summary>
public sealed record TimeProbe(
    DateTime ServerUtc,      // 보정까지 반영한 서버 기준 현재 시각
    DateTime LocalUtc,       // 같은 순간의 PC 시각
    TimeSpan Offset,         // 서버 − PC (양수면 PC 가 느림)
    TimeSpan RoundTrip,      // 왕복 지연
    bool Precise,            // 초가 바뀌는 순간까지 잡아낸 결과인지
    int Attempts);

/// <summary>웹 서버 응답의 Date 헤더로 표준 시각을 읽어온다</summary>
public static class TimeSync
{
    /// <summary>
    /// 지정한 주소의 Date 헤더를 읽어 PC 시각과의 차이를 구한다.
    /// precise 를 켜면 Date 의 초가 넘어가는 순간을 포착해 초 단위 절삭 오차(최대 1초)를 없앤다.
    /// </summary>
    public static async Task<TimeProbe> ProbeAsync(string url, bool precise, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("주소를 입력하세요");
        if (!url.Contains("://")) url = "https://" + url.Trim();

        using var handler = new HttpClientHandler { AllowAutoRedirect = true, UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Key.Macro/1.1 (time sync)");

        var first = await ReadDateAsync(client, url, token);
        if (!precise)
            return Build(first.date, first.sent, first.rtt, false, 1);

        // Date 는 초 단위라, 값이 1초 넘어가는 순간을 잡으면 그 순간이 정확한 초 경계가 된다
        var deadline = Stopwatch.StartNew();
        var previous = first.date;
        int attempts = 1;

        while (deadline.Elapsed < TimeSpan.FromSeconds(2.5) && !token.IsCancellationRequested)
        {
            await Task.Delay(90, token);
            var probe = await ReadDateAsync(client, url, token);
            attempts++;

            if (probe.date > previous)
                return Build(probe.date, probe.sent, probe.rtt, true, attempts);

            previous = probe.date;
        }

        return Build(first.date, first.sent, first.rtt, false, attempts);
    }

    private static TimeProbe Build(DateTime serverDate, DateTime sentLocal, TimeSpan rtt, bool precise, int attempts)
    {
        // 서버가 Date 를 기록한 시점 ≒ 요청 보낸 시각 + 왕복의 절반
        var matchedLocal = sentLocal + TimeSpan.FromTicks(rtt.Ticks / 2);
        var offset = serverDate - matchedLocal;
        var now = DateTime.UtcNow;
        return new TimeProbe(now + offset, now, offset, rtt, precise, attempts);
    }

    private static async Task<(DateTime date, DateTime sent, TimeSpan rtt)> ReadDateAsync(
        HttpClient client, string url, CancellationToken token)
    {
        // 캐시된 응답을 피하려고 매번 다른 쿼리를 붙인다
        var target = url + (url.Contains('?') ? "&" : "?") + "_kmts=" + DateTime.UtcNow.Ticks;

        var sent = DateTime.UtcNow;
        var watch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Head, target);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException)
        {
            // HEAD 를 막는 서버는 GET 으로 헤더만 받아온다
            sent = DateTime.UtcNow;
            watch.Restart();
            using var get = new HttpRequestMessage(HttpMethod.Get, target);
            response = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, token);
        }

        using (response)
        {
            watch.Stop();
            var date = response.Headers.Date
                ?? throw new InvalidOperationException("응답에 Date 헤더가 없습니다. 다른 주소를 입력해 보세요.");
            return (date.UtcDateTime, sent, watch.Elapsed);
        }
    }
}
