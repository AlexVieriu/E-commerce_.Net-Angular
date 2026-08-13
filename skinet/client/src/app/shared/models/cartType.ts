import { CartItem } from './cartItem';
import { Coupon } from './coupon';

export type CartType = {
  items: CartItem[];
  deliveryMethodId?: number;
  paymentIntentId?: string;
  clientSecret?: string;
  coupon?: Coupon;
};
