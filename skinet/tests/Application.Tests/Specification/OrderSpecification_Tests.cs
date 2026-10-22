namespace Application.Tests.Specification;

// Each OrderSpecification factory, run in memory over a list of orders.
public class OrderSpecification_Tests
{
    [Fact]
    public void ById_ShouldFilterByOrderId()
    {
        // Arrange
        var spec = OrderSpecification.ById(1);
        var matchingOrder = CreateOrder(1, "test@test.com");
        var notMatchingOrder = CreateOrder(2, "test@test.com");
        var orders = new List<Order> { matchingOrder, notMatchingOrder }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public void ByEmail_ShouldFilterByBuyerEmail()
    {
        // Arrange
        var emailTest = "test@test.com";
        var spec = OrderSpecification.ByEmail(emailTest);
        var matchingOrder = CreateOrder(1, emailTest);
        var notMatchingOrder = CreateOrder(2, "other@test.com");
        var orders = new List<Order> { matchingOrder, notMatchingOrder }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(emailTest, result[0].BuyerEmail);
    }

    [Fact]
    public void ByEmail_ShouldOrderByOrderDateDescending()
    {
        // Arrange
        var email = "test@test.com";
        var olderOrder = CreateOrder(1, email, orderDate: DateTime.UtcNow.AddDays(-2));
        var newerOrder = CreateOrder(2, email, orderDate: DateTime.UtcNow.AddDays(-1));
        var newestOrder = CreateOrder(3, email, orderDate: DateTime.UtcNow);
        var orders = new List<Order> { olderOrder, newerOrder, newestOrder }.AsQueryable();

        var spec = OrderSpecification.ByEmail(email);

        // Act — full spec: WHERE + ORDER BY
        var result = ApplyFullSpec(spec, orders).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].Id); // Newest first
        Assert.Equal(2, result[1].Id);
        Assert.Equal(1, result[2].Id); // Oldest last
    }

    [Fact]
    public void ByIdAndEmail_ShouldFilterByBothCriteria()
    {
        var email = "test@test.com";
        var spec = OrderSpecification.ByIdAndEmail(email, 2);
        var matchingOrder = CreateOrder(2, email);
        var wrongId = CreateOrder(1, email);
        var wrongEmail = CreateOrder(2, "other@test.com");
        var orders = new List<Order> { matchingOrder, wrongId, wrongEmail }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].Id);
        Assert.Equal(email, result[0].BuyerEmail);
    }

    [Fact]
    public void ByPaymentIntent_ShouldFilterByPaymentIntentId()
    {
        // Arrange
        var paymentIntentId = "pi_abc123";
        var spec = OrderSpecification.ByPaymentIntent(paymentIntentId);
        var matchingOrder = CreateOrder(1, "test@test.com", paymentIntentId);
        var notMatchingOrder = CreateOrder(2, "test@test.com", "pi_xyz789");
        var orders = new List<Order> { matchingOrder, notMatchingOrder }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(paymentIntentId, result[0].PaymentIntentId);
    }

    [Fact]
    public void WithFilters_WithStatusFilter_ShouldFilterByOrderStatus()
    {
        // Arrange
        var specParams = new OrderSpecParams { Status = "Pending", PageIndex = 1, PageSize = 10 };
        var spec = OrderSpecification.WithFilters(specParams);
        var pendingOrder = CreateOrder(1, "test@test.com", status: OrderStatus.Pending);
        var paymentReceivedOrder = CreateOrder(2, "test@test.com", status: OrderStatus.PaymentReceived);
        var orders = new List<Order> { pendingOrder, paymentReceivedOrder }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(OrderStatus.Pending, result[0].Status);
    }

    [Fact]
    public void WithFilters_WithInvalidStatus_ShouldNotFilter()
    {
        // Arrange
        var specParams = new OrderSpecParams { Status = "InvalidStatus", PageIndex = 1, PageSize = 10 };
        var spec = OrderSpecification.WithFilters(specParams);
        var order1 = CreateOrder(1, "test@test.com", status: OrderStatus.Pending);
        var order2 = CreateOrder(2, "test@test.com", status: OrderStatus.PaymentReceived);
        var orders = new List<Order> { order1, order2 }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert - should return all orders when status is invalid
        Assert.Equal(2, result.Count);
    }
    [Fact]
    public void WithFilters_ShouldOrderByOrderDateDescending()
    {
        // Arrange
        var specParams = new OrderSpecParams { PageIndex = 1, PageSize = 10 };
        var spec = OrderSpecification.WithFilters(specParams);
        var olderOrder = CreateOrder(1, "test@test.com", orderDate: DateTime.UtcNow.AddDays(-2));
        var newerOrder = CreateOrder(2, "test@test.com", orderDate: DateTime.UtcNow.AddDays(-1));
        var orders = new List<Order> { olderOrder, newerOrder }.AsQueryable();

        // Act — full spec: WHERE + ORDER BY
        var result = ApplyFullSpec(spec, orders).ToList();

        // Assert
        Assert.Equal(2, result[0].Id); // Newer order first
        Assert.Equal(1, result[1].Id);
    }

    [Fact]
    public void WithFilters_ShouldApplyPagination()
    {
        // Arrange
        var specParams = new OrderSpecParams { PageIndex = 2, PageSize = 2 };
        var spec = OrderSpecification.WithFilters(specParams);
        var orders = new List<Order>
        {
            CreateOrder(1, "test@test.com", orderDate: DateTime.UtcNow.AddDays(-4)),
            CreateOrder(2, "test@test.com", orderDate: DateTime.UtcNow.AddDays(-3)),
            CreateOrder(3, "test@test.com", orderDate: DateTime.UtcNow.AddDays(-2)),
            CreateOrder(4, "test@test.com", orderDate: DateTime.UtcNow.AddDays(-1)),
            CreateOrder(5, "test@test.com", orderDate: DateTime.UtcNow)
        }.AsQueryable();

        // Act — full spec: WHERE + ORDER BY + paging
        var result = ApplyFullSpec(spec, orders).ToList();

        // Assert — newest first is 5, 4, 3, 2, 1; page 2 skips 5 and 4
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[0].Id);
        Assert.Equal(2, result[1].Id);
    }

    [Fact]
    public void WithStatus_ShouldFilterByStatus()
    {
        // Arrange
        var spec = OrderSpecification.ByEmail("test@test.com")
                                     .WithStatus(OrderStatus.PaymentReceived);
        var matchingOrder = CreateOrder(1, "test@test.com", status: OrderStatus.PaymentReceived);
        var notMatchingOrder = CreateOrder(2, "test@test.com", status: OrderStatus.Pending);
        var orders = new List<Order> { matchingOrder, notMatchingOrder }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(OrderStatus.PaymentReceived, result[0].Status);
    }

    [Fact]
    public void WithMinimumTotal_ShouldFilterByMinimumAmount()
    {
        // Arrange
        var spec = OrderSpecification.ByEmail("test@test.com")
                                     .WithMinimumTotal(100m);
        var aboveMinOrder = CreateOrder(1, "test@test.com", subtotal: 150m);
        var atMinOrder = CreateOrder(2, "test@test.com", subtotal: 100m);
        var belowMinOrder = CreateOrder(3, "test@test.com", subtotal: 50m);
        var orders = new List<Order> { aboveMinOrder, atMinOrder, belowMinOrder }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.Id == 1);
        Assert.Contains(result, o => o.Id == 2);
    }

    [Fact]
    public void WithDiscount_ShouldFilterOrdersWithDiscount()
    {
        // Arrange
        var spec = OrderSpecification.ByEmail("test@test.com")
                                     .WithDiscount();
        var withDiscount = CreateOrder(1, "test@test.com", discount: 10m);
        var noDiscount = CreateOrder(2, "test@test.com", discount: 0m);
        var orders = new List<Order> { withDiscount, noDiscount }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
        Assert.True(result[0].Discount > 0);
    }

    [Fact]
    public void ChainedFilters_ShouldApplyAllCriteria()
    {
        // Arrange
        var spec = OrderSpecification.ByEmail("test@test.com")
                                     .WithStatus(OrderStatus.PaymentReceived)
                                     .WithMinimumTotal(100m)
                                     .WithDiscount();

        var matchingOrder = CreateOrder(1, "test@test.com",
            status: OrderStatus.PaymentReceived, subtotal: 150m, discount: 10m);
        var wrongStatus = CreateOrder(2, "test@test.com",
            status: OrderStatus.Pending, subtotal: 150m, discount: 10m);
        var wrongTotal = CreateOrder(3, "test@test.com",
            status: OrderStatus.PaymentReceived, subtotal: 50m, discount: 10m);
        var noDiscount = CreateOrder(4, "test@test.com",
            status: OrderStatus.PaymentReceived, subtotal: 150m, discount: 0m);

        var orders = new List<Order> { matchingOrder, wrongStatus, wrongTotal, noDiscount }.AsQueryable();

        // Act
        var result = spec.ApplyCriteria(orders).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }


    // - - - - - - - - Helper Methods - - - - - - - - 
    private static Order CreateOrder(int id,
                                     string email,
                                     string paymentIntentId = "pi_123",
                                     OrderStatus status = OrderStatus.Pending,
                                     DateTime? orderDate = null,
                                     decimal subtotal = 100m,
                                     decimal discount = 0m)
    => new Order
    {
        Id = id,
        BuyerEmail = email,
        PaymentIntentId = paymentIntentId,
        Status = status,
        OrderDate = orderDate ?? DateTime.UtcNow,
        Subtotal = subtotal,
        Discount = discount,
        OrderItems = [],
        DeliveryMethod = CreateDeliveryMethod()
    };

    private static DeliveryMethod CreateDeliveryMethod() => new DeliveryMethod
    {
        ShortName = "UPS",
        DeliveryTime = "1-2 Days",
        Description = "Fast",
        Price = 10
    };

    // ApplyCriteria() is WHERE only; this adds ORDER BY and paging, as SpecificationEvaluator does in production.
    private static IQueryable<T> ApplyFullSpec<T>(BaseSpecification<T> spec, IQueryable<T> query)
    {
        if (spec.Criteria != null)
            query = query.Where(spec.Criteria);

        if (spec.OrderByDesc != null)
            query = query.OrderByDescending(spec.OrderByDesc);
        else if (spec.OrderByAsc != null)
            query = query.OrderBy(spec.OrderByAsc);

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }
}
