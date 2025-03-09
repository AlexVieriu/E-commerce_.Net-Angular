import { inject, Injectable } from '@angular/core';
import { CartService } from './cart.service';
import { asyncScheduler, forkJoin, scheduled } from 'rxjs';
import { AccountService } from './account.service';

@Injectable({
  providedIn: 'root'
})
export class InitService {
  private cartService = inject(CartService);
  accountService = inject(AccountService);

  init() {
    const cartId = localStorage.getItem('cart_id');
    const cart$ = cartId ? this.cartService.getCartAsync(cartId)
      : scheduled([null], asyncScheduler);

    return forkJoin([cart$, this.accountService.getUserInfo()]);
  }
}