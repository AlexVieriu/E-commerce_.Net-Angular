namespace Core.Specifications;

public class OrderSpecification : BaseSpecification<Order>
{
    // get the list of orders 
    public OrderSpecification(string email) : base(o => o.BuyerEmail == email)
    {
        AddInclude(x => x.OrderItems);
        AddInclude(x => x.DeliveryMethod);
        AddOrderByDescending(x => x.OrderDate);
    }

    // an individual user can only request only their own order
    public OrderSpecification(string email, int id) : base(o => o.BuyerEmail == email && o.Id == id)
    {
        AddInclude("OrderItems");
        AddInclude("DeliveryMethod");
    }
}
