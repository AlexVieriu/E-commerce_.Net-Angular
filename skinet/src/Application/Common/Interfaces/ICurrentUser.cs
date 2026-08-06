namespace Application.Common.Interfaces;

// The authenticated caller. Lets Infrastructure key data by user without referencing API (BUG-18).
public interface ICurrentUser
{
    // The Identity user id. Throws when there is no authenticated user.
    string Id { get; }
}
