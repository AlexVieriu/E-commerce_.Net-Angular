namespace Application.Tests.Common.Mapping;

// INTERMEDIATE EXAMPLE 17: Testing Exception Handling
public class AddressMappingExtensions_Tests
{
    [Fact]
    public void ToEntity_ShouldThrowException_WhenDtoIsNull()
    {
        // Arrange
        AddressDto? nullDto = null;

        // Act
        Action act = () => nullDto!.toEntity();

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("addressDto");
    }

    [Fact]
    public void UpdateFromDto_ShouldUpdateAllProperties()
    {
        // Arrange
        var address = new Address
        {
            Line1 = "Old Address",
            City = "Old City",
            State = "OS",
            PostalCode = "00000",
            Country = "Old Country"
        };

        var dto = new AddressDto
        {
            Line1 = "New Address",
            Line2 = "Apt 123",
            City = "New City",
            State = "NS",
            PostalCode = "99999",
            Country = "New Country"
        };

        // Act
        address.UpdateFromDto(dto);

        // Assert
        address.Line1.Should().Be("New Address");
        address.Line2.Should().Be("Apt 123");
        address.City.Should().Be("New City");
        address.State.Should().Be("NS");
        address.PostalCode.Should().Be("99999");
        address.Country.Should().Be("New Country");
    }
}
