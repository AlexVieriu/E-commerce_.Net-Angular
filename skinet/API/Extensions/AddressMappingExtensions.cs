namespace API.Extensions;

public static class AddressMappingExtensions
{
    public static AddressDto? toDto(this Address? address)
    {
        if (address == null)
            return null;


        AddressDto addressDto = new AddressDto
        {
            City = address.City,
            State = address.State,
            Line1 = address.Line1,
            Line2 = address.Line2,
            Country = address.Country,
            PostalCode = address.PostalCode
        };

        return addressDto;
    }

    public static Address toEntity(this AddressDto addressDto)
    {
        if (addressDto == null)
            throw new ArgumentNullException(nameof(addressDto));

        return new Address
        {
            City = addressDto.City,
            State = addressDto.State,
            Line1 = addressDto.Line1,
            Line2 = addressDto.Line2,
            Country = addressDto.Country,
            PostalCode = addressDto.PostalCode
        };
    }

    public static void UpdateFromDto(this Address address, AddressDto addressDto)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        if (addressDto == null)
            throw new ArgumentNullException(nameof(addressDto));

        address.City = addressDto.City;
        address.State = addressDto.State;
        address.Line1 = addressDto.Line1;
        address.Line2 = addressDto.Line2;
        address.Country = addressDto.Country;
        address.PostalCode = addressDto.PostalCode;
    }
}
