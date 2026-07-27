import { OrderItem } from './orderItem';
import { PaymentSummary } from './paymentSummary';
import { ShippingAddress } from './shippingAddress';

// Mirrors Core.Entities.OrderAggregate.OrderStatus (backend).
export type OrderStatus =
  'Pending' | 'PaymentReceived' | 'PaymentFailed' | 'PaymentMismatch' | 'Refunded';

export interface Order {
  id: number;
  orderDate: string;
  buyerEmail: string;
  shippingAddress: ShippingAddress;
  deliveryMethod: string;
  shippingPrice: number;
  paymentSummary: PaymentSummary;
  orderItems: OrderItem[];
  subtotal: number;
  status: OrderStatus;
  paymentIntentId: string;
  total: number;
  discount?: number;
}
