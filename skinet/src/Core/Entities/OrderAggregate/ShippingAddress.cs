namespace Core.Entities.OrderAggregate;

public class ShippingAddress
{
    public required string Name { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public required string City { get; set; }
    // Optional: stateless countries (e.g. Romania) have no state/region. Enforced per-country in ShippingAddressValidator.
    public string? State { get; set; }
    public required string PostalCode { get; set; }
    public required string Country { get; set; }
}