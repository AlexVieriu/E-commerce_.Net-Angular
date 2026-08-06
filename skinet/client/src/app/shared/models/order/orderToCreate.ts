import { PaymentSummary } from './paymentSummary';
import { ShippingAddress } from './shippingAddress';

export interface OrderToCreate {
  deliveryMethodId?: number;
  shippingAddress: ShippingAddress;
  paymentSummary: PaymentSummary;
}
