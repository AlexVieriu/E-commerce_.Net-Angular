# SignalR

Real-time order completion push via `NotificationHub`.

## NotificationHub

`[Authorize]` — only authenticated users can connect.
Maps email → `ConnectionId` in a static `ConcurrentDictionary`. Removes entry on disconnect.

```csharp
public static string? GetConnectionId(string email)
    => UserConnections.TryGetValue(email, out var id) ? id : string.Empty;
```

Called by `PaymentsController` (after a webhook confirms payment) and `AdminController` (after an
admin-triggered payment sync) to push `OrderCompleteNotification` to the buyer — same event, two triggers.

Registered in `Program.cs`:
```csharp
app.MapHub<NotificationHub>("/hub/notifications");
```

## Client (SignalrService)

```typescript
createHubConnection()  // connects with cookies, listens for 'OrderCompleteNotification'
stopHubConnection()    // stops if Connected
```

`orderSignal = signal<Order | null>(null)` — set when notification arrives, read by checkout-success.
Connection started in `InitService.init()` if user is authenticated, and on `AccountService.login()`.
Guard in `createHubConnection()` prevents duplicate connections — no-op if already `Connected`.

## Rules / Constraints

- `[Authorize]` on the hub — unauthenticated clients are rejected.
- `GetConnectionId` returns empty string (not null) when email not found — check before sending.
- In-memory dictionary doesn't scale across multiple servers — use Redis backplane for scale-out.
- The push always happens in the controller, never in a service — `architecture.md`, *Rules*, explains why.

## Common Mistakes

- Calling `createHubConnection()` before auth is confirmed — hub rejects the connection.
