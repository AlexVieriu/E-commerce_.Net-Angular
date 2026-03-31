import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CartService } from '../services/cart.service';
import { SnackbarService } from '../services/snackbar.service';

export const emptycartGuard: CanActivateFn = (route, state) => {
  const cartService = inject(CartService);
  const router = inject(Router);
  const snackbar = inject(SnackbarService);

  if (!cartService.cart() || cartService.itemCount() === 0) {
    snackbar.error('Your cart is empty');
    router.navigateByUrl('/cart');
    return false;
  }
  return true;
};