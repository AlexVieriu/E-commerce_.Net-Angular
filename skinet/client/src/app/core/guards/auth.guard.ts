import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account.service';
import { inject } from '@angular/core';
import { of, map } from 'rxjs';

// Registered on /checkout and /checkout/success (features/checkout/routes.ts), on /orders
// (features/orders/routes.ts) and on /admin (app.routes.ts).
export const authGuard: CanActivateFn = (_route, state) => {
  const accountService = inject(AccountService);
  const router = inject(Router);

  if (accountService.currentUser()) {
    return of(true);
  }

  // The router subscribes every guard at once and only then picks an answer by array order, so
  // navigating from in here would move the page before the router has decided. Return the
  // destination and let the router own the navigation.
  // The queryParams carry the user back to the page they were trying to reach.
  return accountService
    .getAuthState()
    .pipe(
      map((auth) =>
        auth.isAuthenticated
          ? true
          : router.createUrlTree(['/account/login'], { queryParams: { returnUrl: state.url } })
      )
    );
};
