namespace API.Services;

// Lives in API because HttpContext does. Infrastructure gets the caller through ICurrentUser instead.
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Throws rather than returning null: no HttpContext means no request, so there is no caller.
    public string Id => accessor.HttpContext?.User.GetUserId()
        ?? throw new AuthenticationException("No authenticated user on this request");
}
