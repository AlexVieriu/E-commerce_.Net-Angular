import { AccountService } from '../services/account.service';
import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { of, map } from 'rxjs';

// Registered on /checkout, /checkout/success, /orders and /admin.
export const authGuard: CanActivateFn = (_route, state) => {
  const accountService = inject(AccountService);
  const router = inject(Router);

  if (accountService.currentUser()) {
    return of(true);
  }

  // Return a UrlTree, never navigate: navigate() replaces the navigation before the router picks.
  // returnUrl sends the user back after login.
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
