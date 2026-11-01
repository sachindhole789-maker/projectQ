using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Rest.API.Realtime;

/// <summary>Per-user in-app notifications arrive at <c>/hubs/notifications</c>.</summary>
[Authorize]
public sealed class NotificationHub : Hub;
