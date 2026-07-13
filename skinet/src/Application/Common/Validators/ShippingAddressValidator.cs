using FluentValidation;

namespace Application.Common.Validators;

// Child validator reused via SetValidator on CreateOrderDto.ShippingAddress — demonstrates
// FluentValidation's composability (one address rule set, reusable across DTOs).
public class ShippingAddressValidator : AbstractValidator<ShippingAddress>
{
    // The only countries Stripe itself validates a state for; elsewhere (e.g. Romania) the
    // Address Element legitimately omits it. https://support.stripe.com/questions/connect-address-validation
    private static readonly string[] StateRequiredCountries = ["US", "CA", "AU"];

    public ShippingAddressValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Line1).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.PostalCode).NotEmpty();
        RuleFor(x => x.Country).NotEmpty();

        // State is a US/CA/AU concept — requiring it everywhere 400s stateless countries.
        RuleFor(x => x.State)
            .NotEmpty()
            .When(x => RequiresState(x.Country))
            .WithMessage("State is required for US, Canada and Australia addresses.");
        // Line2 is intentionally optional.
    }

    private static bool RequiresState(string? country)
        => country is not null && StateRequiredCountries.Contains(country, StringComparer.OrdinalIgnoreCase);
}
