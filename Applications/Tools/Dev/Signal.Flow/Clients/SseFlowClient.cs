namespace SignalFlow.Clients;

public class SseFlowClient : IDisposable
{
    private HttpClient?          _http;
    private CancellationTokenSource? _cts;
    private Task?                _readTask;

    public event Action<ServerEvent>? EventReceived;
    public event Action<string>?      StatusChanged;

    public string Status { get; private set; } = "끊김";

    public void Connect(string url)
    {
        if (_cts != null) return;

        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _cts  = new CancellationTokenSource();
        var ct = _cts.Token;

        SetStatus("연결 중");
        _readTask = Task.Run(async () =>
        {
            try
            {
                await using var stream = await _http.GetStreamAsync(url, ct);
                using var reader = new StreamReader(stream);

                SetStatus("연결됨");

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    if (line.StartsWith("data: ", StringComparison.Ordinal))
                    {
                        var json = line["data: ".Length..];
                        try
                        {
                            var evt = JsonSerializer.Deserialize<ServerEvent>(json,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (evt != null) EventReceived?.Invoke(evt);
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)                  { }
            finally
            {
                SetStatus("끊김");
            }
        }, ct);
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _http?.Dispose();
        _http = null;
        SetStatus("끊김");
    }

    private void SetStatus(string s)
    {
        Status = s;
        StatusChanged?.Invoke(s);
    }

    public void Dispose() => Disconnect();
}
