using Order = Core.Entities.OrderAggregate.Order;
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

        mockCartService.Setup(x => x.GetCartAsync())
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

    // ReconcileOrderPayment has no Stripe SDK call (unlike the rest of this class) —
    // it only touches order.GetTotal() and IUnitOfWork, so it's testable without a Stripe sandbox.
    [Fact]
    public async Task ReconcileOrderPayment_AmountMatches_SetsPaymentReceived()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        mockUnitOfWork.Setup(x => x.Complete()).ReturnsAsync(true);

        var sut = new PaymentService(new Mock<ICartService>().Object, mockUnitOfWork.Object,
            Mock.Of<ICouponService>());
        var order = CreateOrder(subtotal: 50m, discount: 0m, deliveryPrice: 10m);

        // total = 60.00 -> 6000 cents
        await sut.ReconcileOrderPayment(order, paidAmountInCents: 6000);

        Assert.Equal(OrderStatus.PaymentReceived, order.Status);
        mockUnitOfWork.Verify(x => x.Complete(), Times.Once);
    }

    [Fact]
    public async Task ReconcileOrderPayment_AmountMismatches_SetsPaymentMismatch()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        mockUnitOfWork.Setup(x => x.Complete()).ReturnsAsync(true);

        var sut = new PaymentService(new Mock<ICartService>().Object, mockUnitOfWork.Object,
            Mock.Of<ICouponService>());
        var order = CreateOrder(subtotal: 50m, discount: 0m, deliveryPrice: 10m);

        // total = 60.00 -> 6000 cents, Stripe paid 5000
        await sut.ReconcileOrderPayment(order, paidAmountInCents: 5000);

        Assert.Equal(OrderStatus.PaymentMismatch, order.Status);
        mockUnitOfWork.Verify(x => x.Complete(), Times.Once);
    }

    private static Order CreateOrder(decimal subtotal, decimal discount, decimal deliveryPrice) => new()
    {
        BuyerEmail = "buyer@test.com",
        PaymentIntentId = "pi_test",
        Subtotal = subtotal,
        Discount = discount,
        DeliveryMethod = new DeliveryMethod
        {
            ShortName = "UPS1",
            DeliveryTime = "1-2 Days",
            Description = "Fast",
            Price = deliveryPrice
        }
    };
}
