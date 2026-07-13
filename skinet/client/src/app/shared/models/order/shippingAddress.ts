export interface ShippingAddress {
    name: string
    line1: string
    line2?: string
    city: string
    // Optional — stateless countries (e.g. Romania) have none. Required for US/CA/AU server-side.
    state?: string
    postalCode: string
    country: string
}