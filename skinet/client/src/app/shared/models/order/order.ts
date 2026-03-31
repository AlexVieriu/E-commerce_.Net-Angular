import { OrderItem } from "./orderItem"
import { PaymentSummary } from "./paymentSummary"
import { ShippingAddress } from "./shippingAddress"

export interface Order {
    id: number
    orderDate: string
    buyerEmail: string
    shippingAddress: ShippingAddress
    deliveryMethod: string
    shippingPrice: number
    paymentSummary: PaymentSummary
    orderItems: OrderItem[]
    subtotal: number
    status: string
    paymentIntentId: string
    total: number
    discount?: number
}