import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account.service';
import { inject } from '@angular/core';
import { of } from 'rxjs/internal/observable/of';
import { map } from 'rxjs/internal/operators/map';

// authGuard is activated in app.routes.ts on the CheckoutComponent route
export const authGuard: CanActivateFn = (route, state) => {
  const accountService = inject(AccountService)
  const router = inject(Router);

  if (accountService.currentUser()) {
    return of(true);
  }
  else {
    // ofGuard wil automatically subscribe/unsubscribe for us
    return accountService.getAuthState().pipe(
      map(auth => {
        if (auth.isAuthenticated) {
          return true;
        } else
          router.navigate(['/account/login'], { queryParams: { returnUrl: state.url } });
        // we will use the queryParams to redirect the user back to the page they were trying to access

        return false;
      })
    )
  }
};
