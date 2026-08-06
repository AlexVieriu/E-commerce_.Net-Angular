namespace API.Services;

// Lives in API because HttpContext does. Infrastructure gets the caller through ICurrentUser instead.
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Throws rather than returning null: a caller with no HttpContext is running outside a request
    // (background work, the Stripe webhook), and there is no cart to resolve for it.
    public string Id => accessor.HttpContext?.User.GetUserId()
        ?? throw new AuthenticationException("No authenticated user on this request");
}
