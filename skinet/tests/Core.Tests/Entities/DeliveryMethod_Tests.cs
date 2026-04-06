namespace Core.Tests.Entities;

// BEGINNER EXAMPLE 12: Testing Object Initialization
public class DeliveryMethod_Tests
{
    [Fact]
    public void DeliveryMethod_ShouldInitializeWithProperties()
    {
        // Arrange & Act
        var deliveryMethod = new DeliveryMethod
        {
            ShortName = "UPS1",
            Description = "Fastest delivery time",
            DeliveryTime = "1-2 Days",
            Price = 10m
        };

        // Assert
        deliveryMethod.Should().NotBeNull();
        deliveryMethod.ShortName.Should().Be("UPS1");
        deliveryMethod.Price.Should().BePositive();
        deliveryMethod.DeliveryTime.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("UPS1", 10)]
    [InlineData("UPS2", 5)]
    [InlineData("UPS", 0)]
    public void DeliveryMethod_ShouldHandleDifferentPrices(string shortName, decimal price)
    {
        // Arrange & Act
        var method = new DeliveryMethod
        {
            ShortName = shortName,
            Description = "Test",
            DeliveryTime = "Test",
            Price = price
        };

        // Assert 
        method.ShortName.Should().Be(shortName);
        method.Price.Should().Be(price);
    }
}
