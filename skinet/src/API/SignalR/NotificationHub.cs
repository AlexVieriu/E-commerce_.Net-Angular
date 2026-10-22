namespace API.SignalR;
// Maps a buyer's email to one connection id, so the server can push to them.
// SignalR's built-in Clients.User(id) does the same by user id, across every tab.
// In memory: a second server instance wouldn't see it.
[Authorize]
public class NotificationHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> UserConnections = new();

    public override Task OnConnectedAsync()
    {
        var email = Context.User?.GetEmail()!;
        if (!string.IsNullOrEmpty(email))
            UserConnections[email] = Context.ConnectionId;

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var email = Context.User?.GetEmail()!;
        if (!string.IsNullOrEmpty(email))
            UserConnections.TryRemove(email, out _);

        return base.OnDisconnectedAsync(exception);
    }

    public static string? GetConnectionId(string email)
        => UserConnections.TryGetValue(email, out var connectionId) ? connectionId : string.Empty;
}