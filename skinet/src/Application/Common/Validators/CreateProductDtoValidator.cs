using FluentValidation;

namespace Application.Common.Validators;

public class CreateProductDtoValidator : AbstractValidator<CreateProductDTO>
{
    public CreateProductDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");
        RuleFor(x => x.PictureUrl).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Brand).NotEmpty();
        RuleFor(x => x.QuantityInStock)
            .GreaterThanOrEqualTo(1).WithMessage("Quantity in stock must be at least 1");
    }
}