import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { NavigationExtras, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SnackbarService } from '../services/snackbar.service';
import { ValidationError } from '../../shared/models/validationError';
import { AccountService } from '../services/account.service';

// Translates a status into a message or a destination.
// Domain work belongs to the service that owns the request.
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const snackbar = inject(SnackbarService);
  const accountService = inject(AccountService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      // The body can be empty. An unguarded deref throws a TypeError that replaces the
      // HttpErrorResponse, so every instanceof check downstream stops matching.
      const message = err.error?.title || err.error?.message || err.error;

      if (err.status === 400) {
        if (err.error?.errors) {
          const modelStateErrors = (Object.values(err.error.errors) as string[][]).flat();
          return throwError(() => new ValidationError(modelStateErrors));
        } else {
          snackbar.error(message || 'Bad request');
        }
      }
      if (err.status === 401) {
        // Session is gone (cookie expired/cleared) — drop stale client auth state
        // so the navbar updates from username back to Login/Register.
        accountService.clearCurrentUser();
        const alreadyOnLogin = router.url.includes('/account/login');
        if (!alreadyOnLogin)
          router.navigate(['/account/login'], { queryParams: { returnUrl: router.url } });
      }
      if (err.status === 403) {
        snackbar.error('Forbidden'); // we don't have error message for "Forbidden" status
      }
      if (err.status === 409) {
        // A stale checkout cart and an admin edit conflict both land here.
        // The message is all they share; CheckoutConflictService reloads the cart.
        snackbar.error(message || 'Conflict');
      }
      if (err.status === 404) {
        router.navigateByUrl('/not-found');
      }
      if (err.status === 500) {
        const navigationExtras: NavigationExtras = { state: { error: err.error } };
        router.navigateByUrl('/server-error', navigationExtras);
      }
      return throwError(() => err);
    })
  );
};
