import { describe, expect, it, beforeEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { CheckoutConflictService } from './checkout-conflict.service';
import { OrderService } from './order.service';
import { environment } from '../../../environments/environment';

// The 409 recovery lives here now, not in errorInterceptor.
// Pins both halves: the recovery, and the funnel that triggers it.
describe('CheckoutConflictService', () => {
  let service: CheckoutConflictService;
  let httpTesting: HttpTestingController;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    service = TestBed.inject(CheckoutConflictService);
    httpTesting = TestBed.inject(HttpTestingController);
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl');
  });

  it('refreshes the cart from the server, then leaves for the cart page', () => {
    service.recoverStaleCart();

    httpTesting.expectOne(environment.apiUrl + 'cart').flush({ items: [] });

    expect(navigateByUrl).toHaveBeenCalledWith('/cart');
  });

  // A failed refresh must not leave the customer on a checkout priced from a dead cart.
  it('leaves for the cart page even when the refresh fails', () => {
    service.recoverStaleCart();

    httpTesting
      .expectOne(environment.apiUrl + 'cart')
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(navigateByUrl).toHaveBeenCalledWith('/cart');
  });
});

// StripeService funnels POST /payments the same way, but loadStripe() in its constructor
// leaves no DI seam to test through (docs/testing.md).
describe('OrderService funnels POST /orders into the recovery', () => {
  let orderService: OrderService;
  let httpTesting: HttpTestingController;
  const conflictService = { recoverStaleCart: vi.fn() };

  const createOrder = () =>
    new Promise<unknown>((resolve) =>
      orderService.createOrder({} as never).subscribe({ error: resolve })
    );

  beforeEach(() => {
    conflictService.recoverStaleCart.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CheckoutConflictService, useValue: conflictService }
      ]
    });

    orderService = TestBed.inject(OrderService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  it('recovers when the order 409s on a price change', async () => {
    const done = createOrder();
    httpTesting
      .expectOne(environment.apiUrl + 'orders')
      .flush({ message: 'Prices have changed' }, { status: 409, statusText: 'Conflict' });

    await done;

    expect(conflictService.recoverStaleCart).toHaveBeenCalledOnce();
  });

  it('leaves every other failure alone', async () => {
    const done = createOrder();
    httpTesting
      .expectOne(environment.apiUrl + 'orders')
      .flush({ message: 'nope' }, { status: 400, statusText: 'Bad Request' });

    await done;

    expect(conflictService.recoverStaleCart).not.toHaveBeenCalled();
  });
});
