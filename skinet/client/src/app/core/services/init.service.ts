import { inject, Injectable } from '@angular/core';
import { CartService } from './cart.service';
import { asyncScheduler, catchError, forkJoin, of, scheduled, tap } from 'rxjs';
import { AccountService } from './account.service';
import { SignalrService } from './signalr.service';
import { HttpErrorResponse } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class InitService {
  private cartService = inject(CartService);
  private accountService = inject(AccountService);
  private signalrService = inject(SignalrService);

  init() {
    const cartId = localStorage.getItem('cart_id');
    const cart$ = cartId ? this.cartService.getCartAsync(cartId)
      : scheduled([null], asyncScheduler);

    return forkJoin({
      cart: cart$,
      user: this.accountService.getUserInfo().pipe(
        catchError((err: HttpErrorResponse) => {
          if (err.status === 401) return of(null);
          throw err;
        }),
        tap(user => {
          if (user) this.signalrService.createHubConnection();
        })
      )
    });
  }
}