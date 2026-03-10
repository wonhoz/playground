using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace SignalFlow.Clients;

public class SignalRFlowClient : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<ServerEvent>? EventReceived;
    public event Action<string>?      StatusChanged;

    public string Status { get; private set; } = "끊김";

    public async Task ConnectAsync(string url)
    {
        if (_connection != null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                // SignalR 서버는 camelCase로 직렬화하므로 대소문자 무시 설정 필수
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        _connection.On<ServerEvent>("Event", e => EventReceived?.Invoke(e));

        _connection.Reconnecting  += _ => { SetStatus("재연결 중"); return Task.CompletedTask; };
        _connection.Reconnected   += _ => { SetStatus("연결됨");    return Task.CompletedTask; };
        _connection.Closed        += _ => { SetStatus("끊김");      return Task.CompletedTask; };

        await _connection.StartAsync();
        SetStatus("연결됨");
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _connection = null;
        SetStatus("끊김");
    }

    private void SetStatus(string s)
    {
        Status = s;
        StatusChanged?.Invoke(s);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
