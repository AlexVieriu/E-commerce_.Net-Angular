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
    public void ToEntity_ShouldMapAllFields_WhenDtoIsValid()
    {
        var dto = new AddressDto
        {
            Line1 = "123 Main St",
            Line2 = "Apt 4B",
            City = "London",
            State = "England",
            PostalCode = "SW1A 1AA",
            Country = "GB"
        };

        var entity = dto.toEntity();

        entity.Line1.Should().Be("123 Main St");
        entity.Line2.Should().Be("Apt 4B");
        entity.City.Should().Be("London");
        entity.State.Should().Be("England");
        entity.PostalCode.Should().Be("SW1A 1AA");
        entity.Country.Should().Be("GB");
    }

    [Fact]
    public void ToDto_ShouldReturnNull_WhenAddressIsNull()
    {
        Address? nullAddress = null;

        var result = nullAddress.toDto();

        result.Should().BeNull();
    }

    [Fact]
    public void ToDto_ShouldMapAllFields_WhenAddressIsValid()
    {
        var address = new Address
        {
            Line1 = "456 High St",
            Line2 = null,
            City = "Manchester",
            State = "England",
            PostalCode = "M1 1AE",
            Country = "GB"
        };

        var dto = address.toDto();

        dto.Should().NotBeNull();
        dto!.Line1.Should().Be("456 High St");
        dto.Line2.Should().BeNull();
        dto.City.Should().Be("Manchester");
        dto.State.Should().Be("England");
        dto.PostalCode.Should().Be("M1 1AE");
        dto.Country.Should().Be("GB");
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
