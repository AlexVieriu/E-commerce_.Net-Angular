using Application.Common.Validators;
using FluentValidation.TestHelper;

namespace Application.Tests.Common.DTOs;

public class CreateOrderDto_Validation_Tests
{
    private readonly CreateOrderDtoValidator _validator = new();

    private static CreateOrderDto ValidDto() => new()
    {
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

    // ShouldFail_WhenCartIdIsEmpty is gone with the property: the DTO no longer carries a cart id,
    // so there is nothing to validate (BUG-18).

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

    // BUG-11: State is a US/CA/AU concept — requiring it everywhere 400s stateless countries.
    [Theory]
    [InlineData("RO")]   // Romania — Stripe's Address Element omits state entirely
    [InlineData("GB")]
    [InlineData("FR")]
    public void ShouldPass_WhenStateIsMissing_ForStatelessCountry(string country)
    {
        var dto = ValidDto();
        dto.ShippingAddress.Country = country;
        dto.ShippingAddress.State = null;

        _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("CA")]
    [InlineData("AU")]
    [InlineData("us")]   // Stripe sends ISO codes; the rule must not be case-sensitive
    public void ShouldFail_WhenStateIsMissing_ForStateRequiringCountry(string country)
    {
        var dto = ValidDto();
        dto.ShippingAddress.Country = country;
        dto.ShippingAddress.State = null;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ShippingAddress.State");
    }
}
