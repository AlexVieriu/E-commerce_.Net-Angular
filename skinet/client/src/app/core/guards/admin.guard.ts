import { CanActivateFn, Router } from '@angular/router';
import { AccountService } from '../services/account.service';
import { inject } from '@angular/core';
import { SnackbarService } from '../services/snackbar.service';

export const adminGuard: CanActivateFn = () => {
  const accountService = inject(AccountService);
  const router = inject(Router);
  const snackBar = inject(SnackbarService);

  if (accountService.isAdmin()) return true;

  // An anonymous visitor is short of a session, not a role.
  // authGuard is already sending them to login with a returnUrl.
  if (accountService.currentUser()) {
    snackBar.error("You don't have permission to access this page");
  }

  return router.createUrlTree(['/account/login']);
};
