import { Pipe, PipeTransform } from '@angular/core';
import { ConfirmationToken } from '@stripe/stripe-js';
import { ShippingAddress } from '../models/order/shippingAddress';

@Pipe({
  name: 'address'
})
export class AddressPipe implements PipeTransform {

  transform(value?: ConfirmationToken['shipping'] | ShippingAddress, ...args: unknown[]): unknown {
    if (value && 'address' in value && value.name) {

      // ConfirmationToken -> 'shipping' = PaymentIntent.Shipping -> Address -> line1, line2, ...
      // Declaring multiple individual constants by extracting from the object
      // Object destructing
      const { line1, line2, city, state, postal_code, country } =
        (value as ConfirmationToken['shipping'])?.address!;

      return this.format([value.name, line1, line2, city, state, postal_code, country]);
    }
    else if (value && 'line1' in value) {
      const { line1, line2, city, state, postalCode, country } = (value as ShippingAddress)!;

      return this.format([value.name, line1, line2, city, state, postalCode, country]);
    }
    else
      return 'Unknown Address';
  }

  // Drop empty parts (line2, and state for stateless countries) so we never render ", ,".
  private format(parts: (string | null | undefined)[]): string {
    return parts.filter(part => !!part?.trim()).join(', ');
  }
}

