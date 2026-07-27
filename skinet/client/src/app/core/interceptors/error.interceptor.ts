import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { NavigationExtras, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SnackbarService } from '../services/snackbar.service';
import { ValidationError } from '../../shared/models/validationError';
import { CartService } from '../services/cart.service';
import { AccountService } from '../services/account.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const snackbar = inject(SnackbarService);
  const cartService = inject(CartService);
  const accountService = inject(AccountService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 400) {
        if (err.error.errors) {
          const modelStateErrors = (Object.values(err.error.errors) as string[][]).flat();
          throw new ValidationError(modelStateErrors);
        } else {
          snackbar.error(err.error.title || err.error.message || err.error);
        }
      }
      if (err.status === 401) {
        // Session is gone (cookie expired/cleared) — drop stale client auth state
        // so the navbar updates from username back to Login/Register.
        accountService.clearCurrentUser();
        const alreadyOnLogin = router.url.includes('/account/login');
        if (!req.url.includes('account/user-info') && !alreadyOnLogin)
          router.navigate(['/account/login'], { queryParams: { returnUrl: router.url } });
      }
      if (err.status === 403) {
        snackbar.error('Forbidden'); // we don't have error message for "Forbidden" status
      }
      if (err.status === 409) {
        snackbar.error(err.error.message);
        // Two conflicts share 409: checkout's price change (POST /orders) and a product-edit concurrency loss.
        // Only checkout touches the cart.
        if (req.method === 'POST' && req.url.endsWith('/orders')) {
          const cartId = cartService.cart()?.id;
          const goToCart = () => router.navigateByUrl('/cart');

          if (cartId)
            cartService.getCartAsync(cartId).subscribe(goToCart);
          else
            goToCart();
        }
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
