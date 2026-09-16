import { inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { CartService } from './cart.service';

// A 409 on checkout means the server re-priced the cart.
// The interceptor shows the message; recovering the cart belongs to whoever owns the request.
@Injectable({
  providedIn: 'root'
})
export class CheckoutConflictService {
  private cartService = inject(CartService);
  private router = inject(Router);

  // Refresh first — prices, coupon and clientSecret have all moved.
  // Navigate either way, so a failed refresh doesn't strand the customer on checkout.
  recoverStaleCart() {
    const goToCart = () => this.router.navigateByUrl('/cart');
    this.cartService.getCartAsync().subscribe({ next: goToCart, error: goToCart });
  }
}
