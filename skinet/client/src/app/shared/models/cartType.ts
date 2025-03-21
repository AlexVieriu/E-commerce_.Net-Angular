import { CartItem } from "./cartItem"

export type CartType = {
    id: string,
    items: CartItem[],
    deliveryMethodId?: number,
    paymentIntentId?: string,
    clientSecret?: string,
}