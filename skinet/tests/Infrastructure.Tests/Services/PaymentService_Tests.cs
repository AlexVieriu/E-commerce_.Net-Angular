using Order = Core.Entities.OrderAggregate.Order;
namespace Infrastructure.Tests.Services;

// Testing with Multiple Mocks
public class PaymentService_Tests
{
    // ⚠ Arrange only — no Act, no Assert, so it always passes; the Act needs a Stripe sandbox.
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

        // TODO: Act + Assert, once the Stripe calls can be mocked (IStripeClient).
    }

    // ReconcileOrderPayment makes no Stripe call — only GetTotal() and IUnitOfWork — so it is testable.
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
