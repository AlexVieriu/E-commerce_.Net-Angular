import { Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { CartService } from '../../../core/services/cart.service';
import { CommonModule, CurrencyPipe, Location } from '@angular/common';
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
    CommonModule,
    MatIcon],
  templateUrl: './order-summary.component.html',
  styleUrl: './order-summary.component.scss'
})
export class OrderSummaryComponent {
  cartService = inject(CartService);
  location = inject(Location); //  from angular/common

  private stripeService = inject(StripeService);
  couponCode?: string;

  applyCouponCode() {
    // set the cart with the coupon if valid
    // if in checkout update the payment intent (hint: this returns an observable so

    if (!this.couponCode) return;

    this.cartService.applyDiscount(this.couponCode).subscribe({
      next: async coupon => {
        const cart = this.cartService.cart();
        if (cart) {
          cart.coupon = coupon;
          await firstValueFrom(this.cartService.setCart(cart)); 
          this.couponCode = undefined;// remove the coupon code
          if (this.location.path() === '/checkout') {
            await firstValueFrom(this.stripeService.createOrUpdatePaymentIntent());
          }
        }
      }
    });
  }

  async removeCouponCode() {
    // remove coupon from cart
    // if in checkout update the payment intent 

    const cart = this.cartService.cart();
    if (!cart) return;

    if (cart.coupon) cart.coupon = undefined;
    await firstValueFrom(this.cartService.setCart(cart)); // save cart in redis DB
    if (this.location.path() === '/checkout') {
      await firstValueFrom(this.stripeService.createOrUpdatePaymentIntent());
    }
  }
}
