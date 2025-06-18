namespace API.SignalR;
// This hub enables sending notifications to users by their email address
// SignalR natively only tracks client connection IDs 
// (generated identifiers used by browsers, to maintain WebSocket connections), 
// not user identities like email addresses
// To map email addresses to connection IDs, we store this relationship in memory using
// a thread-safe ConcurrentDictionary 
// For production scaling across multiple servers, this should be replaced with a distributed cache like Redis
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
