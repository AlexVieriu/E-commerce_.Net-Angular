import { AccountService } from '../services/account.service';
import { CanActivateFn, Router } from '@angular/router';
import { CartService } from '../services/cart.service';
import { inject } from '@angular/core';
import { SnackbarService } from '../services/snackbar.service';

export const emptycartGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const cartService = inject(CartService);
  const router = inject(Router);
  const snackbar = inject(SnackbarService);

  // An anonymous visitor never has a cart, so this check would always fail for them.
  // authGuard sends them to login; this guard answers only for logged-in visitors.
  if (accountService.currentUser()) {
    if (!cartService.cart() || cartService.itemCount() === 0) {
      snackbar.error('Your cart is empty');
      return router.createUrlTree(['/cart']);
    }
  }

  return true;
};
