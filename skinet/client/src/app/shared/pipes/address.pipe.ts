import { Pipe, PipeTransform } from '@angular/core';
import { ConfirmationToken } from '@stripe/stripe-js';
import { ShippingAddress } from '../models/order/shippingAddress';

@Pipe({
  name: 'address'
})
export class AddressPipe implements PipeTransform {
  transform(value?: ConfirmationToken['shipping'] | ShippingAddress): unknown {
    // Stripe's shape — the parts live on a nested `address` object.
    // Guard `value.address` itself: `'address' in value` only proves the key exists, not that it's set.
    if (value && 'address' in value && value.address && value.name) {
      const { line1, line2, city, state, postal_code, country } = value.address;

      return this.format([value.name, line1, line2, city, state, postal_code, country]);
    }
    // Our own shape — the parts sit flat on the object.
    else if (value && 'line1' in value) {
      const { line1, line2, city, state, postalCode, country } = value;

      return this.format([value.name, line1, line2, city, state, postalCode, country]);
    } else return 'Unknown Address';
  }

  // Drop empty parts (line2, and state for stateless countries) so we never render ", ,".
  private format(parts: (string | null | undefined)[]): string {
    return parts.filter((part) => !!part?.trim()).join(', ');
  }
}
