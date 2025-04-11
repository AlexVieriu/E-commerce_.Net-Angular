import { inject, Injectable } from '@angular/core';
import { CartService } from './cart.service';
import { asyncScheduler, forkJoin, scheduled, tap } from 'rxjs';
import { AccountService } from './account.service';
import { SignalrService } from './signalr.service';

@Injectable({
  providedIn: 'root'
})
export class InitService {
  private cartService = inject(CartService);
  accountService = inject(AccountService);
  signalrService = inject(SignalrService);

  init() {
    const cartId = localStorage.getItem('cart_id');
    const cart$ = cartId ? this.cartService.getCartAsync(cartId)
      : scheduled([null], asyncScheduler);

    return forkJoin({
      cart: cart$,
      user: this.accountService.getUserInfo().pipe(
        tap(user => {
          if (user) this.signalrService.createHubConnection();
        })
      )
    });
  }
}