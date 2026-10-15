import { describe, expect, it, beforeEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { throwError } from 'rxjs';
import { errorInterceptor } from './error.interceptor';
import { SnackbarService } from '../services/snackbar.service';
import { AccountService } from '../services/account.service';
import { ValidationError } from '../../shared/models/validationError';

// The interceptor translates a status; it does not run domain work.
// A 409 used to reload the cart here, gated on a URL string — these specs pin that out.
describe('errorInterceptor', () => {
  const snackbar = { error: vi.fn(), success: vi.fn() };
  const accountService = { clearCurrentUser: vi.fn() };

  let router: Router;
  let navigate: ReturnType<typeof vi.spyOn>;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  // A POST to the endpoint the old URL gate matched on.
  const run = (err: HttpErrorResponse, url = '/api/orders') =>
    TestBed.runInInjectionContext(() =>
      errorInterceptor(new HttpRequest('POST', url, {}), () => throwError(() => err))
    );

  const caught = async (err: HttpErrorResponse, url?: string) =>
    new Promise<unknown>((resolve) => run(err, url).subscribe({ error: resolve }));

  beforeEach(() => {
    snackbar.error.mockClear();
    accountService.clearCurrentUser.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: SnackbarService, useValue: snackbar },
        { provide: AccountService, useValue: accountService }
      ]
    });

    router = TestBed.inject(Router);
    navigate = vi.spyOn(router, 'navigate');
    navigateByUrl = vi.spyOn(router, 'navigateByUrl');
  });

  describe('409 Conflict', () => {
    const conflict = new HttpErrorResponse({
      status: 409,
      error: { message: 'Prices have changed' }
    });

    it('shows the message the server sent', async () => {
      await caught(conflict);

      expect(snackbar.error).toHaveBeenCalledWith('Prices have changed');
    });

    // The point of the move: recovery belongs to the service that owns the request.
    it('does not navigate, whichever endpoint 409d', async () => {
      await caught(conflict);
      await caught(conflict, '/api/products/5');

      expect(navigate).not.toHaveBeenCalled();
      expect(navigateByUrl).not.toHaveBeenCalled();
    });

    it('re-throws so the caller can recover', async () => {
      expect(await caught(conflict)).toBe(conflict);
    });
  });

  describe('401 Unauthorized', () => {
    const unauthorized = new HttpErrorResponse({ status: 401 });

    it('drops client auth state and sends the visitor to login with a returnUrl', async () => {
      await caught(unauthorized);

      expect(accountService.clearCurrentUser).toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/account/login'], {
        queryParams: { returnUrl: router.url }
      });
    });

    it('stays put when the visitor is already on the login page', async () => {
      vi.spyOn(router, 'url', 'get').mockReturnValue('/account/login');

      await caught(unauthorized);

      expect(accountService.clearCurrentUser).toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });
  });

  describe('400 Bad Request', () => {
    it('turns model-state errors into a ValidationError', async () => {
      const error = await caught(
        new HttpErrorResponse({ status: 400, error: { errors: { Email: ['Email is taken'] } } })
      );

      expect(error).toBeInstanceOf(ValidationError);
      expect((error as ValidationError).errors).toEqual(['Email is taken']);
    });

    // An empty body used to throw a TypeError that replaced the HttpErrorResponse,
    // so `error instanceof HttpErrorResponse` downstream stopped matching.
    it('survives an empty body and keeps the original error', async () => {
      const empty = new HttpErrorResponse({ status: 400, error: null });

      expect(await caught(empty)).toBe(empty);
      expect(snackbar.error).toHaveBeenCalledWith('Bad request');
    });
  });
});
