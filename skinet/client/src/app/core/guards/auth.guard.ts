import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account.service';
import { inject } from '@angular/core';
import { of, map } from 'rxjs';

// Registered on /checkout, /checkout/success, /orders and /admin.
export const authGuard: CanActivateFn = (_route, state) => {
  const accountService = inject(AccountService);
  const router = inject(Router);

  if (accountService.currentUser()) {
    return of(true);
  }

  // The router runs every guard at once, then picks an answer by array order.
  // Navigating from in here would move the page before the router has decided.
  // returnUrl brings the user back after login.
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
