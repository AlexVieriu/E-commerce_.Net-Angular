namespace Infrastructure.Tests.Services;

// INTERMEDIATE EXAMPLE 14: Testing with Multiple Mocks
public class PaymentService_Tests
{
    // Note: Actual test would need Stripe SDK setup 
    // We do only the Mocking part
    [Fact]
    public async Task CreateOrUpdatePaymentIntent_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(x => x["StripeSettings:SecretKey"])
                  .Returns("sk_test_dummy");

        var mockCartService = new Mock<ICartService>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();

        var cart = new ShoppingCart
        {
            Id = "test-cart",
            Items = new List<CartItem>
            {
                new() {
                    ProductId = 1,
                    ProductName = "Test",
                    Price = 100m,
                    Quantity = 2,
                    PictureUrl = "/test.png",
                    Brand = "Test",
                    Type = "Test"
                }
            },
            DeliveryMethodId = 1
        };

        mockCartService.Setup(x => x.GetCartAsync("test-cart"))
                       .ReturnsAsync(cart);

        // Mock delivery method repository
        var mockDeliveryRepo = new Mock<IGenericRepository<DeliveryMethod>>();
        mockDeliveryRepo.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(new DeliveryMethod
            {
                ShortName = "UPS1",
                DeliveryTime = "1-2 Days",
                Price = 10m,
                Description = "Fast"
            });

        mockUnitOfWork.Setup(x => x.Repository<DeliveryMethod>())
                      .Returns(mockDeliveryRepo.Object);

        // ToDo: Needs Stripe SDK setup and implementation                           
    }
}
