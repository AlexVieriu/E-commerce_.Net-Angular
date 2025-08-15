import { PaymentSummary } from "./paymentSummary";
import { ShippingAddress } from "./shippingAddress";

export interface OrderToCreate {
    cartId: string;
    deliveryMethodId?: number;
    shippingAddress: ShippingAddress
    paymentSummary: PaymentSummary
    discount?: number
}