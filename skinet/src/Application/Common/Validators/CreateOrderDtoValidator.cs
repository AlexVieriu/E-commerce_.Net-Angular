using FluentValidation;

namespace Application.Common.Validators;

public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.DeliveryMethodId)
            .GreaterThan(0).WithMessage("A delivery method must be selected");
        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .SetValidator(new ShippingAddressValidator());
        RuleFor(x => x.PaymentSummary).NotNull();
    }
}
