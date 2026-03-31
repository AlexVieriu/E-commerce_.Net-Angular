namespace Core.Tests.Entities;

// BEGINNER EXAMPLE 8: Testing Value Objects
// Why this tests?
// make a full object test 
// test difference scenarios with one property
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
        payment.ExpYear.Should().BeGreaterThan(2025);
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
