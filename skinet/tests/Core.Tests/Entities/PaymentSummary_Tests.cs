namespace Core.Tests.Entities;

// PaymentSummary as a value object: one full object, then one property per scenario.
public class PaymentSummary_Tests
{
    [Fact]
    public void PaymentSummary_ShouldStoreCardDetails()
    {
        // Arrange & Act
        var payment = new PaymentSummary
        {
            Last4 = 4242,
            Brand = "Visa",
            ExpMonth = 12,
            ExpYear = 2025
        };

        // Assert
        payment.Last4.Should().Be(4242);
        payment.Brand.Should().Be("Visa");
        payment.ExpMonth.Should().BeInRange(1, 12);
        payment.ExpYear.Should().Be(2025);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void PaymentSummary_ExpMonth_ShouldBeValid(int month)
    {
        // Arrange & Act
        var payment = new PaymentSummary
        {
            Last4 = 4242,
            Brand = "Visa",
            ExpMonth = month,
            ExpYear = 2025
        };

        // Assert
        payment.ExpMonth.Should().BeInRange(1, 12);
    }
}
