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

  // Logout nulls the user and the cart together, so a logged-out visitor always looks cart-less.
  // authGuard owns that case; answering here would give the wrong reason.
  if (!accountService.currentUser()) return true;

  if (!cartService.cart() || cartService.itemCount() === 0) {
    snackbar.error('Your cart is empty');
    return router.createUrlTree(['/cart']);
  }

  return true;
};
