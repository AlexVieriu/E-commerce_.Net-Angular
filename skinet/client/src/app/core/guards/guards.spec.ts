import { describe, expect, it, beforeEach, vi } from 'vitest';
import { provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree
} from '@angular/router';
import { firstValueFrom, isObservable, Observable, of } from 'rxjs';
import { authGuard } from './auth.guard';
import { adminGuard } from './admin.guard';
import { emptycartGuard } from './emptycart.guard';
import { orderCompleteGuard } from './order-complete.guard';
import { AccountService } from '../services/account.service';
import { CartService } from '../services/cart.service';
import { OrderService } from '../services/order.service';
import { SnackbarService } from '../services/snackbar.service';
import { User } from '../../shared/models/user';
import { Cart } from '../../shared/models/cart';

// BUG-17: the router runs every guard at once, then picks by array order.
// A guard that navigates itself moves the page first, so it cannot lose.
// These specs pin both halves: return a destination, never navigate.

const currentUser = signal<User | null>(null);
const isAdmin = signal(false);
const cart = signal<Cart | null>(null);
const itemCount = signal(0);
const authState = signal(false);

const orderService = { orderComplete: false };
const snackbar = { error: vi.fn(), success: vi.fn() };

const accountServiceStub = {
  currentUser,
  isAdmin,
  getAuthState: () => of({ isAuthenticated: authState() })
};

const cartServiceStub = { cart, itemCount };

const loggedIn = { email: 'a@b.com', firstName: 'A', lastName: 'B', roles: [] } as unknown as User;

/** Guards return `boolean | UrlTree`, sometimes in an Observable. Flatten both. */
const resolve = async (result: unknown) =>
  isObservable(result) ? await firstValueFrom(result as Observable<boolean | UrlTree>) : result;

describe('route guards return a destination instead of navigating (BUG-17)', () => {
  let router: Router;
  let navigate: ReturnType<typeof vi.spyOn>;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  const state = { url: '/checkout' } as RouterStateSnapshot;
  const route = {} as ActivatedRouteSnapshot;

  beforeEach(() => {
    currentUser.set(null);
    isAdmin.set(false);
    cart.set(null);
    itemCount.set(0);
    authState.set(false);
    orderService.orderComplete = false;
    snackbar.error.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AccountService, useValue: accountServiceStub },
        { provide: CartService, useValue: cartServiceStub },
        { provide: OrderService, useValue: orderService },
        { provide: SnackbarService, useValue: snackbar }
      ]
    });

    router = TestBed.inject(Router);
    navigate = vi.spyOn(router, 'navigate');
    navigateByUrl = vi.spyOn(router, 'navigateByUrl');
  });

  const expectNoImperativeNavigation = () => {
    expect(navigate).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  };

  describe('authGuard', () => {
    const run = () => TestBed.runInInjectionContext(() => authGuard(route, state));

    it('allows a user already in the signal without asking the server', async () => {
      currentUser.set(loggedIn);

      expect(await resolve(run())).toBe(true);
    });

    it('sends an anonymous visitor to login, carrying the returnUrl', async () => {
      const result = (await resolve(run())) as UrlTree;

      expect(result).toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(result)).toBe('/account/login?returnUrl=%2Fcheckout');
      expectNoImperativeNavigation();
    });

    it('allows a visitor the server still recognises', async () => {
      authState.set(true);

      expect(await resolve(run())).toBe(true);
    });
  });

  describe('emptycartGuard', () => {
    const run = () => TestBed.runInInjectionContext(() => emptycartGuard(route, state));

    // Logout nulls the user and the cart together, so this guard must not answer.
    it('stands down for an anonymous visitor and says nothing', async () => {
      expect(await resolve(run())).toBe(true);
      expect(snackbar.error).not.toHaveBeenCalled();
    });

    it('sends a logged-in visitor with no cart to the cart page', async () => {
      currentUser.set(loggedIn);

      const result = (await resolve(run())) as UrlTree;

      expect(result).toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(result)).toBe('/cart');
      expect(snackbar.error).toHaveBeenCalledWith('Your cart is empty');
      expectNoImperativeNavigation();
    });

    it('sends a logged-in visitor with an empty cart to the cart page', async () => {
      currentUser.set(loggedIn);
      cart.set({ id: 'cart-1', items: [] } as unknown as Cart);

      expect(await resolve(run())).toBeInstanceOf(UrlTree);
    });

    it('allows a logged-in visitor holding items', async () => {
      currentUser.set(loggedIn);
      cart.set({ id: 'cart-1', items: [{}] } as unknown as Cart);
      itemCount.set(1);

      expect(await resolve(run())).toBe(true);
      expect(snackbar.error).not.toHaveBeenCalled();
    });
  });

  describe('adminGuard', () => {
    const run = () => TestBed.runInInjectionContext(() => adminGuard(route, state));

    it('allows an admin', async () => {
      isAdmin.set(true);

      expect(await resolve(run())).toBe(true);
    });

    it('tells a signed-in non-admin why, and returns login', async () => {
      currentUser.set(loggedIn);

      const result = (await resolve(run())) as UrlTree;

      expect(router.serializeUrl(result)).toBe('/account/login');
      expect(snackbar.error).toHaveBeenCalledWith("You don't have permission to access this page");
      expectNoImperativeNavigation();
    });

    // authGuard already explains this one, with a returnUrl attached.
    it('stays quiet for an anonymous visitor', async () => {
      expect(await resolve(run())).toBeInstanceOf(UrlTree);
      expect(snackbar.error).not.toHaveBeenCalled();
    });
  });

  describe('orderCompleteGuard', () => {
    const run = () => TestBed.runInInjectionContext(() => orderCompleteGuard(route, state));

    it('allows the page once an order has completed', async () => {
      orderService.orderComplete = true;

      expect(await resolve(run())).toBe(true);
    });

    it('returns the shop when no order has completed', async () => {
      const result = (await resolve(run())) as UrlTree;

      expect(router.serializeUrl(result)).toBe('/shop');
      expectNoImperativeNavigation();
    });
  });
});
