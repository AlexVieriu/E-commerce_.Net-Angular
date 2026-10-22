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
    // Auth first: the cart needs a user.
    // No 401 to catch: an anonymous caller gets 204, which arrives as null.
    return this.accountService.getUserInfo().pipe(
      tap((user) => {
        if (user) this.signalrService.createHubConnection();
      }),
      // No user, no cart.
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
