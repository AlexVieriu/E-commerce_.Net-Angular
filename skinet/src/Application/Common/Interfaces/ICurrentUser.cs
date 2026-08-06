namespace Application.Common.Interfaces;

// The authenticated caller, resolved server-side. Lets Infrastructure key data by user without
// referencing the API project, so a request can never name a resource it doesn't own (BUG-18).
public interface ICurrentUser
{
    // The Identity user id. Throws when there is no authenticated user.
    string Id { get; }
}
