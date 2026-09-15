import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CartService } from '../services/cart.service';
import { SnackbarService } from '../services/snackbar.service';
import { AccountService } from '../services/account.service';

export const emptycartGuard: CanActivateFn = () => {
  const cartService = inject(CartService);
  const router = inject(Router);
  const snackbar = inject(SnackbarService);
  const accountService = inject(AccountService);

  // Logging out nulls the user and the cart in one step, so an anonymous visitor always looks
  // cart-less. authGuard owns that case — stand down rather than answer with the wrong reason.
  // This also keeps the outcome correct if the canActivate array is ever reordered.
  if (!accountService.currentUser()) return true;

  if (!cartService.cart() || cartService.itemCount() === 0) {
    snackbar.error('Your cart is empty');
    return router.createUrlTree(['/cart']);
  }

  return true;
};
