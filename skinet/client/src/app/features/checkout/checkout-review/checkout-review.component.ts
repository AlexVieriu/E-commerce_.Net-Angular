import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { CartService } from '../../../core/services/cart.service';
import { ConfirmationToken } from '@stripe/stripe-js/dist/api/confirmation-tokens';
import { AddressPipe } from "../../../shared/pipes/address.pipe";
import { CardPipe } from "../../../shared/pipes/card.pipe";

@Component({
  selector: 'app-checkout-review',
  imports: [CurrencyPipe, AddressPipe, CardPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout-review.component.html',
  styleUrl: './checkout-review.component.scss'
})
export class CheckoutReviewComponent {
  cartService = inject(CartService);
  confirmationToken = input<ConfirmationToken>();
}
