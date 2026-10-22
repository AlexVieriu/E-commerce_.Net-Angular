import { AccountService } from '../services/account.service';
import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { SnackbarService } from '../services/snackbar.service';

export const adminGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const router = inject(Router);
  const snackBar = inject(SnackbarService);

  if (accountService.isAdmin()) return true;

  // Anonymous visitors are handled by authGuard, so only signed-in users get the message.
  if (accountService.currentUser()) {
    snackBar.error("You don't have permission to access this page");
  }

  return router.createUrlTree(['/account/login']);
};
