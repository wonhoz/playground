using Microsoft.AspNetCore.SignalR;

namespace SignalFlow.Server;

// 서버→클라이언트 단방향 허브
// IHubContext<EventHub>를 통해 외부에서 SendAsync 호출
public class EventHub : Hub { }
