import { Pipe, PipeTransform } from '@angular/core';
import { ConfirmationToken } from '@stripe/stripe-js';

@Pipe({
  name: 'address'
})
export class AddressPipe implements PipeTransform {

  transform(value?: ConfirmationToken['shipping'], ...args: unknown[]): unknown {
    if (value?.address && value.name) {

      // ConfirmationToken -> 'shipping' = PaymentIntent.Shipping -> Address -> line1, line2, ...
      // Declaring multiple individual constants by extracting from the object
      // Object destructing
      const { line1, line2, city, state, postal_code, country } = value.address;
      return `${value.name}, ${line1}${line2 ? ', ' + line2 : ''}, 
              ${city}, ${state}, ${postal_code}, ${country}`;
    } else {
      return 'Unknown Address';
    }
  }
}

