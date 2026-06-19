using FluentValidation;

namespace Application.Common.Validators;

// Child validator reused via SetValidator on CreateOrderDto.ShippingAddress — demonstrates
// FluentValidation's composability (one address rule set, reusable across DTOs).
public class ShippingAddressValidator : AbstractValidator<ShippingAddress>
{
    public ShippingAddressValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Line1).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.State).NotEmpty();
        RuleFor(x => x.PostalCode).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();
        // Line2 is intentionally optional.
    }
}
