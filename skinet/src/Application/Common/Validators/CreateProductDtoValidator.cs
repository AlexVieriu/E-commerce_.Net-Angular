using FluentValidation;

namespace Application.Common.Validators;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDTO>
{
    public CreateProductDtoValidator()
    {
        // Mirrors ProductConfiguration's HasMaxLength — without it an over-long value is a 500, not a 400.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");
        RuleFor(x => x.PictureUrl).NotEmpty();
        RuleFor(x => x.Type).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(30);
        RuleFor(x => x.QuantityInStock)
            .GreaterThanOrEqualTo(1).WithMessage("Quantity in stock must be at least 1");
    }
}