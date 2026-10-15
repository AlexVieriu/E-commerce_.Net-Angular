import { describe, expect, it, beforeEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { OrderSummaryComponent } from './order-summary.component';
import { StripeService } from '../../../core/services/stripe.service';
import { environment } from '../../../../environments/environment';

// errorInterceptor shows the coupon error and rethrows it, so the subscriber must handle it.
// Vitest fails the run on an unhandled error, so this spec fails if the error callback goes.
describe('OrderSummaryComponent.applyCouponCode', () => {
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [OrderSummaryComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        // The real service calls loadStripe() in its constructor; nothing here reaches Stripe.
        { provide: StripeService, useValue: {} }
      ]
    });

    httpTesting = TestBed.inject(HttpTestingController);
  });

  it('swallows a rejected code instead of leaving it uncaught, and keeps the typed code', () => {
    const component = TestBed.createComponent(OrderSummaryComponent).componentInstance;
    component.couponCode.set('nonsense123');

    component.applyCouponCode();
    httpTesting
      .expectOne(environment.apiUrl + 'coupons/nonsense123')
      .flush({ message: 'Invalid voucher code' }, { status: 400, statusText: 'Bad Request' });

    expect(component.couponCode()).toBe('nonsense123');
    httpTesting.verify();
  });
});
