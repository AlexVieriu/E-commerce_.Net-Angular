import { inject, Injectable } from '@angular/core';
import { CartService } from './cart.service';
import { asyncScheduler, scheduled } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class InitService {
  cartService = inject(CartService);

  init() {
    const cartId = localStorage.getItem('cart_id');
    const cart$ = cartId ? this.cartService.getCartAsync(cartId)
      : scheduled([null], asyncScheduler);

    return cart$;
  }
}