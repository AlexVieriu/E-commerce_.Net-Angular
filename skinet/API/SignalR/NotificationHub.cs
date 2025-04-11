namespace API.SignalR;
// We need to have the ability to notify the user based on the email address 
// SignalR itself, doesn't keep track of witch EmailAddresses/User is connected, it just keep track of the
// client connection id, what a browser uses to maintain the connection with our SignalR service
// In order for us, to keep track of how is connected by email, we need to store that inside our hub, so
// it's going to be stored in memory, but for scaling purposes, we need to use Redis Db
// But for simplicity, we are going to use a ConcurrentDictionary
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
            // removes the email key from our user connection
            UserConnections.TryRemove(email, out _);

        return base.OnDisconnectedAsync(exception);
    }

    public static string? GetConnectionId(string email)
        => UserConnections.TryGetValue(email, out var connectionId) ? connectionId : string.Empty;
}
