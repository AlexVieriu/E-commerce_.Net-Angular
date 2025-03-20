import { CurrencyPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { CartService } from '../../../core/services/cart.service';

@Component({
  selector: 'app-checkout-review',
  imports: [CurrencyPipe],
  templateUrl: './checkout-review.component.html',
  styleUrl: './checkout-review.component.scss'
})
export class CheckoutReviewComponent {
  cartService = inject(CartService)
}
