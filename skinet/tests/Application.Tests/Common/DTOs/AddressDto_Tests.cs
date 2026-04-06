namespace Application.Tests.Common.DTOs;

// BEGINNER EXAMPLE 7: Testing DTOs with Data Annotations
public class AddressDto_Tests
{
    [Fact]
    public void AddressDto_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var address = new AddressDto
        {
            Line1 = "123 Main St",
            City = "Test City",
            State = "TS",
            PostalCode = "12345",
            Country = "Test Country"
        };

        // Assert
        address.Line1.Should().NotBeNullOrEmpty();
        address.City.Should().NotBeNullOrEmpty();
        address.State.Should().NotBeNullOrEmpty();
        address.PostalCode.Should().NotBeNullOrEmpty();
        address.Country.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AddressDto_Line2_ShouldBeOptional()
    {
        // Arrange & Act
        var address = new AddressDto
        {
            Line1 = "123 Main St",
            Line2 = null, // Optional
            City = "Test City",
            State = "TS",
            PostalCode = "12345",
            Country = "Test Country"
        };

        // Assert
        address.Line2.Should().BeNull();
    }
}
