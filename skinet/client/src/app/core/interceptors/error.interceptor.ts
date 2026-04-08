import { HttpErrorResponse, HttpEvent, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { NavigationExtras, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SnackbarService } from '../services/snackbar.service';
import { ValidationError } from '../../shared/models/validationError';
import { CartService } from '../services/cart.service';


export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const snackbar = inject(SnackbarService);
  const cartService = inject(CartService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 400) {
        if (err.error.errors) {
          const modelStateErrors = [];
          for (const key in err.error.errors) {
            if (err.error.errors[key]) {
              modelStateErrors.push(err.error.errors[key])
            }
          }
          throw new ValidationError(modelStateErrors.flat()); // flat(): have a single array of strings
        } else {
          snackbar.error(err.error.title || err.error.message || err.error);
        }
      }
      if (err.status === 401) {
        const alreadyOnLogin = router.url.includes('/account/login');
        if (!req.url.includes('account/user-info') && !alreadyOnLogin)
          router.navigate(['/account/login'], { queryParams: { returnUrl: router.url } });
      }
      if (err.status === 403) {
        snackbar.error("Forbidden"); // we don't have error message for "Forbidden" status
      }
      if (err.status === 409) {
        snackbar.error(err.error.message);
        const cartId = cartService.cart()?.id;
        if (cartId) {
          cartService.getCartAsync(cartId).subscribe(() => router.navigateByUrl('/cart'));
        } else {
          router.navigateByUrl('/cart');
        }
      }
      if (err.status === 404) {
        router.navigateByUrl('/not-found');
      }
      if (err.status === 500) {
        const navigationExtras: NavigationExtras = { state: { error: err.error } }
        router.navigateByUrl('/server-error', navigationExtras);
      }
      return throwError(() => err)
    })
  )
};