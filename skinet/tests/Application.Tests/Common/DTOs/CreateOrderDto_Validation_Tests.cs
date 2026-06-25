using Application.Common.Validators;
using FluentValidation.TestHelper;

namespace Application.Tests.Common.DTOs;

public class CreateOrderDto_Validation_Tests
{
    private readonly CreateOrderDtoValidator _validator = new();

    private static CreateOrderDto ValidDto() => new()
    {
        CartId = "cart-123",
        DeliveryMethodId = 1,
        ShippingAddress = new ShippingAddress
        {
            Name = "John Doe",
            Line1 = "123 Main St",
            City = "Test City",
            State = "TS",
            PostalCode = "12345",
            Country = "Test Country"
        },
        PaymentSummary = new PaymentSummary
        {
            Last4 = 4242,
            Brand = "visa",
            ExpMonth = 12,
            ExpYear = 2030
        }
    };

    [Fact]
    public void ShouldPass_WhenAllFieldsAreValid()
    {
        _validator.TestValidate(ValidDto()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ShouldFail_WhenCartIdIsEmpty()
    {
        var dto = ValidDto();
        dto.CartId = "";

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.CartId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldFail_WhenDeliveryMethodIdIsNotPositive(int badId)
    {
        var dto = ValidDto();
        dto.DeliveryMethodId = badId;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DeliveryMethodId);
    }

    [Fact]
    public void ShouldFail_WhenShippingAddressIsNull()
    {
        var dto = ValidDto();
        dto.ShippingAddress = null!;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ShippingAddress);
    }

    [Fact]
    public void ShouldFail_WhenShippingAddressCityIsEmpty()
    {
        var dto = ValidDto();
        dto.ShippingAddress.City = "";

        // Child-validator error surfaces under the parent property path.
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ShippingAddress.City");
    }

    [Fact]
    public void ShouldFail_WhenPaymentSummaryIsNull()
    {
        var dto = ValidDto();
        dto.PaymentSummary = null!;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.PaymentSummary);
    }
}
