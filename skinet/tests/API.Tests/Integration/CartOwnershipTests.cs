namespace API.Tests.Integration;

// BUG-18 at the API layer: two real users, two cookie jars, one Redis.
// The unit tests prove CartService derives the key; these prove the derivation survives the whole
// pipeline — cookie auth, model binding, DI — which is where the bug actually lived.
public class CartOwnershipTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static ShoppingCart CartWith(string claimedId) => new()
    {
        // The server ignores this. Sent anyway because that is the thing under test.
        Id = claimedId,
        Items =
        [
            new CartItem
            {
                ProductId = 1,
                ProductName = "Test Product",
                Price = 100m,
                Quantity = 2,
                PictureUrl = "/test.png",
                Brand = "Test",
                Type = "Test"
            }
        ]
    };

    [Fact]
    public async Task GetCart_ReturnsTheCallersOwnCart_NotAnotherUsers()
    {
        // Arrange: two separate people, each with their own cookie
        var alice = await factory.CreateAuthenticatedClientAsync();
        var mallory = await factory.CreateAuthenticatedClientAsync();

        var saved = await alice.PostAsJsonAsync("api/cart", CartWith("cart:alice"));
        saved.EnsureSuccessStatusCode();

        // Act: Mallory asks for "the cart"
        var mallorysCart = await mallory.GetFromJsonAsync<ShoppingCart>("api/cart");

        // Assert: she gets her own, which is empty — Alice's two items are not in it
        mallorysCart.Should().NotBeNull();
        mallorysCart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCart_IgnoresACartIdSmuggledInTheQueryString()
    {
        var alice = await factory.CreateAuthenticatedClientAsync();
        var mallory = await factory.CreateAuthenticatedClientAsync();

        (await alice.PostAsJsonAsync("api/cart", CartWith("cart:alice"))).EnsureSuccessStatusCode();

        // The old exploit, verbatim: pass someone else's key on the query string.
        // The parameter no longer exists, so the value is inert.
        var response = await mallory.GetFromJsonAsync<ShoppingCart>("api/cart?id=cart:alice");

        response.Should().NotBeNull();
        response!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PostCart_StoresUnderTheCaller_SoTwoUsersNeverShareACart()
    {
        var alice = await factory.CreateAuthenticatedClientAsync();
        var mallory = await factory.CreateAuthenticatedClientAsync();

        // Both claim the same cart id. Under the old design this was one cart.
        (await alice.PostAsJsonAsync("api/cart", CartWith("cart:shared"))).EnsureSuccessStatusCode();
        (await mallory.PostAsJsonAsync("api/cart", CartWith("cart:shared"))).EnsureSuccessStatusCode();

        // Mallory empties "hers"
        (await mallory.DeleteAsync("api/cart")).EnsureSuccessStatusCode();

        // Alice's is untouched
        var alicesCart = await alice.GetFromJsonAsync<ShoppingCart>("api/cart");

        alicesCart.Should().NotBeNull();
        alicesCart!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCart_Returns401_WhenNotAuthenticated()
    {
        var anonymous = factory.CreateWebClient();

        var response = await anonymous.GetAsync("api/cart");

        // No cookie means no cart to derive — the endpoint must not fall back to anything.
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
