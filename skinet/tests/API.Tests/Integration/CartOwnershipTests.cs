namespace API.Tests.Integration;

// BUG-18 at the API layer: two real users, two cookie jars, one Redis.
// The unit tests prove CartService derives the key; these prove it survives the real pipeline.
public class CartOwnershipTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    // xUnit v3 wants every cancellable call to take the test's token (xUnit1051).
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // An anonymous object, not a ShoppingCart, so it can carry an `id` the server type doesn't have —
    // what a hand-rolled attacker would send. These tests prove the property is simply dropped.
    private static object CartClaiming(string id) => new
    {
        id,
        items = new[]
        {
            new
            {
                productId = 1,
                productName = "Test Product",
                price = 100m,
                quantity = 2,
                pictureUrl = "/test.png",
                brand = "Test",
                type = "Test"
            }
        }
    };

    [Fact]
    public async Task GetCart_ReturnsTheCallersOwnCart_NotAnotherUsers()
    {
        // Arrange: two separate people, each with their own cookie
        var alice = await factory.CreateAuthenticatedClientAsync();
        var mallory = await factory.CreateAuthenticatedClientAsync();

        var saved = await alice.PostAsJsonAsync("api/cart", CartClaiming("cart:alice"), Ct);
        saved.EnsureSuccessStatusCode();

        // Act: Mallory asks for "the cart"
        var mallorysCart = await mallory.GetFromJsonAsync<ShoppingCart>("api/cart", Ct);

        // Assert: she gets her own, which is empty — Alice's two items are not in it
        mallorysCart.Should().NotBeNull();
        mallorysCart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCart_IgnoresACartIdSmuggledInTheQueryString()
    {
        var alice = await factory.CreateAuthenticatedClientAsync();
        var mallory = await factory.CreateAuthenticatedClientAsync();

        (await alice.PostAsJsonAsync("api/cart", CartClaiming("cart:alice"), Ct)).EnsureSuccessStatusCode();

        // The old exploit, verbatim: pass someone else's key on the query string.
        // The parameter no longer exists, so the value is inert.
        var response = await mallory.GetFromJsonAsync<ShoppingCart>("api/cart?id=cart:alice", Ct);

        response.Should().NotBeNull();
        response!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PostCart_StoresUnderTheCaller_SoTwoUsersNeverShareACart()
    {
        var alice = await factory.CreateAuthenticatedClientAsync();
        var mallory = await factory.CreateAuthenticatedClientAsync();

        // Both claim the same cart id. Under the old design this was one cart.
        (await alice.PostAsJsonAsync("api/cart", CartClaiming("cart:shared"), Ct)).EnsureSuccessStatusCode();
        (await mallory.PostAsJsonAsync("api/cart", CartClaiming("cart:shared"), Ct)).EnsureSuccessStatusCode();

        // Mallory empties "hers"
        (await mallory.DeleteAsync("api/cart", Ct)).EnsureSuccessStatusCode();

        // Alice's is untouched
        var alicesCart = await alice.GetFromJsonAsync<ShoppingCart>("api/cart", Ct);

        alicesCart.Should().NotBeNull();
        alicesCart!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCart_Returns401_WhenNotAuthenticated()
    {
        var anonymous = factory.CreateWebClient();

        var response = await anonymous.GetAsync("api/cart", Ct);

        // No cookie means no cart to derive — the endpoint must not fall back to anything.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
