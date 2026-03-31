import { Pipe, PipeTransform } from '@angular/core';
import { ConfirmationToken } from '@stripe/stripe-js/dist/api/confirmation-tokens';
import { PaymentSummary } from '../models/order/paymentSummary';

@Pipe({
  name: 'paymentCard'
})
export class CardPipe implements PipeTransform {
  transform(value?: ConfirmationToken['payment_method_preview'] | PaymentSummary, ...args: unknown[]): unknown {
    if (value && 'card' in value && value.card) {
      const { brand, last4, exp_month, exp_year } =
        (value as ConfirmationToken['payment_method_preview']).card!;

      return `${brand.toUpperCase()} **** **** **** ${last4} , Exp: ${exp_month}/${exp_year}`;
    }
    else if (value && 'last4' in value) {
      const { brand, last4, expMonth, expYear } = value as PaymentSummary;
      return `${brand.toUpperCase()} **** **** **** ${last4} , Exp: ${expMonth}/${expYear}`;
    }
    else
      return 'Unknown payment method';
  }
}
