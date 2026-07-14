import { inject, Injectable } from '@angular/core';
import { CartService } from './cart.service';
import { catchError, map, of, switchMap, tap } from 'rxjs';
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

    // Step 1: check auth first — cart requires authentication
    return this.accountService.getUserInfo().pipe(
      catchError((err: HttpErrorResponse) => {
        if (err.status === 401) return of(null);
        throw err;
      }),
      tap((user) => {
        if (user) this.signalrService.createHubConnection();
      }),
      // Step 2: only fetch cart if authenticated and cart_id exists
      switchMap((user) => {
        if (!user || !cartId) return of({ user, cart: null });
        return this.cartService.getCartAsync(cartId).pipe(
          map((cart) => ({ user, cart })),
          catchError(() => {
            localStorage.removeItem('cart_id');
            return of({ user, cart: null });
          })
        );
      })
    );
  }
}
