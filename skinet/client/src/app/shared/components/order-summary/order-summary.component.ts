import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { CartService } from '../../../core/services/cart.service';
import { CurrencyPipe, Location } from '@angular/common';
import { StripeService } from '../../../core/services/stripe.service';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';

@Component({
  selector: 'app-order-summary',
  imports: [
    MatButton,
    RouterLink,
    MatFormField,
    MatLabel,
    MatInput,
    CurrencyPipe,
    FormsModule,
    MatIcon
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-summary.component.html',
  styleUrl: './order-summary.component.scss'
})
export class OrderSummaryComponent {
  cartService = inject(CartService);
  location = inject(Location); //  from angular/common

  private stripeService = inject(StripeService);
  couponCode = signal('');

  applyCouponCode() {
    if (!this.couponCode()) return;

    this.cartService.applyDiscount(this.couponCode()).subscribe({
      next: async () => {
        this.couponCode.set('');
        if (this.location.path() === '/checkout')
          await firstValueFrom(this.stripeService.createOrUpdatePaymentIntent());
      },
      // errorInterceptor already showed the reason ("Invalid voucher code").
      // Handling it here only stops the rethrown error landing in the console as "Uncaught".
      error: () => undefined
    });
  }

  async removeCouponCode() {
    await firstValueFrom(this.cartService.removeCoupon());
    if (this.location.path() === '/checkout')
      await firstValueFrom(this.stripeService.createOrUpdatePaymentIntent());
  }
}
