using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WorkshopOS.Api.Hubs;

[Authorize]
public sealed class WorkshopHub : Hub
{
    public override Task OnConnectedAsync() => base.OnConnectedAsync();
}
