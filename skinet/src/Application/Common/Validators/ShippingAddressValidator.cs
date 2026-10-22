using FluentValidation;

namespace Application.Common.Validators;

// Child validator, plugged into CreateOrderDto.ShippingAddress with SetValidator.
public class ShippingAddressValidator : AbstractValidator<ShippingAddress>
{
    // Stripe validates a state only for these three (stated for Connect onboarding); Romania, e.g., has none.
    // https://support.stripe.com/questions/connect-address-validation
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
