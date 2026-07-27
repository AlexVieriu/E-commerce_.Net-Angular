import { Pipe, PipeTransform } from '@angular/core';
import { ConfirmationToken } from '@stripe/stripe-js/dist/api/confirmation-tokens';
import { PaymentSummary } from '../models/order/paymentSummary';

@Pipe({
  name: 'paymentCard'
})
export class CardPipe implements PipeTransform {
  transform(value?: ConfirmationToken['payment_method_preview'] | PaymentSummary): unknown {
    // Stripe's shape — card details nested under `card`.
    if (value && 'card' in value && value.card) {
      const { brand, last4, exp_month, exp_year } = value.card;

      return `${brand.toUpperCase()} **** **** **** ${last4} , Exp: ${exp_month}/${exp_year}`;
    }
    // Our own shape — card details sit flat on the object.
    else if (value && 'last4' in value) {
      const { brand, last4, expMonth, expYear } = value;
      return `${brand.toUpperCase()} **** **** **** ${last4} , Exp: ${expMonth}/${expYear}`;
    } else return 'Unknown payment method';
  }
}
