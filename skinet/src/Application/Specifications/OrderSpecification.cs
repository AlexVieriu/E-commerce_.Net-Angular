namespace Application.Specifications;

public class OrderSpecification : BaseSpecification<Order>
{
    // Constructor with criteria: When you always have a filter (ById, ForUser, etc.)
    // Empty constructor + AddCriteria: When criteria is optional/conditional (WithFilters)
    private OrderSpecification() : base()
    {
        AddDefaultIncludes();
    }

    private OrderSpecification(Expression<Func<Order, bool>> criteria) : base(criteria)
    {
        AddDefaultIncludes();
    }

    // We use static and non-static methods, to differentiate critical methods and optional ones

    // - - - - - - - - -  Critical Filters - - - - - - - - - 
    public static OrderSpecification ById(int id)
        => new OrderSpecification(o => o.Id == id);

    // Instead of immediately executing orders.Where(o => o.BuyerEmail == "john@example.com"), 
    // you're storing the instructions for that filter so Entity Framework can translate 
    // it to SQL later:  SELECT * FROM Orders WHERE BuyerEmail = 'john@example.com' ORDER BY OrderDate DESC
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

    // For Admin
    public static OrderSpecification WithFilters(OrderSpecParams specParams)
    {
        var spec = new OrderSpecification();

        // Apply status filter if provided
        if (!string.IsNullOrEmpty(specParams.Status))
        {
            var status = ParseOrderStatus(specParams.Status);
            if (status.HasValue)
                spec.AddCriteria(o => o.Status == status.Value);
        }

        // Apply ordering
        spec.AddOrderByDescending(o => o.OrderDate);

        // Apply pagination 
        spec.ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);

        return spec;
    }

    // - - - - - - - - -  Optional Filters - - - - - - - - -
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