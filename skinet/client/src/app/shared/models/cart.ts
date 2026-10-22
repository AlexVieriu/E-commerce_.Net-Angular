import { CartItem } from './cartItem';
import { CartType } from './cartType';
import { Coupon } from './coupon';

// No id: the server keys the cart by the logged-in user, so the client never names it (BUG-18).
export class Cart implements CartType {
  items: CartItem[] = [];

  // Checkout fields: delivery + Stripe.
  deliveryMethodId?: number;
  paymentIntentId?: string;
  clientSecret?: string;
  coupon?: Coupon;
}
