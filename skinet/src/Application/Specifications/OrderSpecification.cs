namespace Application.Specifications;

public class OrderSpecification : BaseSpecification<Order>
{
    // Criteria constructor for a filter that's always there (ById, ByEmail).
    // Empty constructor + AddCriteria for optional filters (WithFilters).
    private OrderSpecification() : base()
    {
        AddDefaultIncludes();
    }

    private OrderSpecification(Expression<Func<Order, bool>> criteria) : base(criteria)
    {
        AddDefaultIncludes();
    }

    // - - - - - - - - -  Critical Filters (static factories) - - - - - - - - -
    public static OrderSpecification ById(int id)
        => new OrderSpecification(o => o.Id == id);

    // The lambdas don't run here; they are stored as expression trees.
    // EF later turns it into WHERE BuyerEmail = @email ORDER BY OrderDate DESC.
    public static OrderSpecification ByEmail(string email)
    {
        var spec = new OrderSpecification(o => o.BuyerEmail == email);
        spec.AddOrderByDescending(o => o.OrderDate);
        return spec;
    }

    public static OrderSpecification ByIdAndEmail(string email, int id)
        => new OrderSpecification(o => o.BuyerEmail == email && o.Id == id);

    public static OrderSpecification ByPaymentIntent(string paymentIntentId)
        => new OrderSpecification(o => o.PaymentIntentId == paymentIntentId);

    // Admin orders list: optional status filter, newest first, paged.
    public static OrderSpecification WithFilters(OrderSpecParams specParams)
    {
        var spec = new OrderSpecification();

        if (!string.IsNullOrEmpty(specParams.Status))
        {
            var status = ParseOrderStatus(specParams.Status);
            if (status.HasValue)
                spec.AddCriteria(o => o.Status == status.Value);
        }

        spec.AddOrderByDescending(o => o.OrderDate);

        spec.ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

        return spec;
    }

    // For the admin dashboard: all orders, newest first. Reuses the default includes —
    // DeliveryMethod is needed for GetTotal(); OrderItems ride along (negligible at this scale).
    public static OrderSpecification AllForDashboard()
    {
        var spec = new OrderSpecification();
        spec.AddOrderByDescending(o => o.OrderDate);
        return spec;
    }

    // - - - - - - - - -  Optional Filters (chainable) - - - - - - - - -
    public OrderSpecification WithStatus(OrderStatus status)
    {
        AddCriteria(o => o.Status == status);
        return this;
    }
    public OrderSpecification WithMinimumTotal(decimal amount)
    {
        AddCriteria(o => o.Subtotal >= amount);
        return this;
    }
    public OrderSpecification WithDiscount()
    {
        AddCriteria(o => o.Discount > 0);
        return this;
    }

    private void AddDefaultIncludes()
    {
        AddInclude(o => o.OrderItems);
        AddInclude(o => o.DeliveryMethod);
    }

    private static OrderStatus? ParseOrderStatus(string status)
        => Enum.TryParse<OrderStatus>(status, true, out var result) ? result : null;
}