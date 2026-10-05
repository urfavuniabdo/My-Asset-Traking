using AssetTracking.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AssetTracking.Web.Hubs;

/// <summary>
/// SignalR Hub للإشعارات اللحظية — الجرس يتحدث بدون Refresh.
/// كل مستخدم ينضم لمجموعة باسم مُعرِّفه فقط، فلا يستقبل إشعارات غيره.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(userId));

        await base.OnDisconnectedAsync(exception);
    }

    public static string GroupName(string userId) => $"user:{userId}";
}

/// <summary>ينفّذ البث الفوري عبر SignalR</summary>
public class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotifier(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task PushToUserAsync(string userId, object payload)
        => _hub.Clients.Group(NotificationHub.GroupName(userId))
               .SendAsync("ReceiveNotification", payload);
}
