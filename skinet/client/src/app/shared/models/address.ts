export type Address = {
    line1: string;
    line2?: string;
    city: string;
    // Optional — matches AddressDto.State (string?), nullable since MakeAddressStateOptional.
    state?: string;
    postalCode: string;
    country: string;
}
