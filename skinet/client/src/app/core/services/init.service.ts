import { inject, Injectable } from '@angular/core';
import { CartService } from './cart.service';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { AccountService } from './account.service';
import { SignalrService } from './signalr.service';

@Injectable({
  providedIn: 'root'
})
export class InitService {
  private cartService = inject(CartService);
  private accountService = inject(AccountService);
  private signalrService = inject(SignalrService);

  init() {
    // Step 1: check auth first — cart requires authentication.
    // No 401 to catch: user-info answers an anonymous caller with 204, which arrives as null.
    return this.accountService.getUserInfo().pipe(
      tap((user) => {
        if (user) this.signalrService.createHubConnection();
      }),
      // Step 2: the cart belongs to the user, so being authenticated is the whole condition
      switchMap((user) => {
        if (!user) return of({ user, cart: null });
        return this.cartService.getCartAsync().pipe(
          map((cart) => ({ user, cart })),
          catchError(() => of({ user, cart: null }))
        );
      })
    );
  }
}
