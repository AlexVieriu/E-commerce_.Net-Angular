namespace Core.Specifications;

public class OrderSpecification : BaseSpecification<Order>
{
    public OrderSpecification(int id) : base(o =>
        o.Id == id)
    {
        AddInclude("OrderItems");
        AddInclude("DeliveryMethod");
    }

    // get the list of orders 
    public OrderSpecification(string email) : base(o =>
        o.BuyerEmail == email)
    {
        AddInclude(x => x.OrderItems);
        AddInclude(x => x.DeliveryMethod);
        AddOrderByDescending(x => x.OrderDate);
    }

    // an individual user can only request only their own order
    public OrderSpecification(string email, int id) : base(o =>
        o.BuyerEmail == email && o.Id == id)
    {
        AddInclude("OrderItems");
        AddInclude("DeliveryMethod");
    }

    public OrderSpecification(string paymentIntentId, bool? isPaymentIntent) : base(x =>
        x.PaymentIntentId == paymentIntentId)
    {
        AddInclude("OrderItems");
        AddInclude("DeliveryMethod");
    }

    public OrderSpecification(OrderSpecParams specParams) : base(o =>
        string.IsNullOrEmpty(specParams.Status) || o.Status == ParseStatus(specParams.Status))
    {
        AddInclude("OrderItems");
        AddInclude("DeliveryMethod");
        ApplyPaging(specParams.PageSize * (specParams.PageIndex - 1), specParams.PageSize);
        AddOrderByDescending(x => x.OrderDate);
    }


    private static OrderStatus? ParseStatus(string status)
    {
        var orderStatus = Enum.TryParse<OrderStatus>(status, true, out var result) ? result : (OrderStatus?)null;

        return orderStatus;
    }

}

