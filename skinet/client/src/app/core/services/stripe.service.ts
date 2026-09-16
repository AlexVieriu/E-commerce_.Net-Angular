import { inject, Injectable } from '@angular/core';
import {
  ConfirmationToken,
  loadStripe,
  Stripe,
  StripeAddressElement,
  StripeAddressElementOptions,
  StripeElements,
  StripePaymentElement
} from '@stripe/stripe-js';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { CartService } from './cart.service';
import { Cart } from '../../shared/models/cart';
import { catchError, firstValueFrom, tap, throwError } from 'rxjs';
import { AccountService } from './account.service';
import { CheckoutConflictService } from './checkout-conflict.service';

@Injectable({
  providedIn: 'root'
})
export class StripeService {
  baseUrl = environment.apiUrl;

  private cartService = inject(CartService);
  private accountService = inject(AccountService);
  private checkoutConflict = inject(CheckoutConflictService);
  private http = inject(HttpClient);

  private stripePromise: Promise<Stripe | null>;
  private elements?: StripeElements;
  private addressElement?: StripeAddressElement;
  private paymentElement?: StripePaymentElement;

  constructor() {
    this.stripePromise = loadStripe(environment.stripePublicKey);
  }

  getStripeInstance() {
    return this.stripePromise;
  }

  async initializeElements() {
    if (!this.elements) {
      const stripe = await this.getStripeInstance();
      if (stripe) {
        const cart = await firstValueFrom(this.createOrUpdatePaymentIntent());
        this.elements = stripe.elements({
          clientSecret: cart.clientSecret,
          appearance: { labels: 'floating' }
        });
      } else {
        throw new Error('Stripe has not been loaded');
      }
    }
    return this.elements;
  }

  async createPaymentElement() {
    if (!this.paymentElement) {
      const elements = await this.initializeElements();
      if (elements) {
        // .create(..) ->  https://docs.stripe.com/payments/payment-element
        this.paymentElement = elements.create('payment');
      } else {
        throw new Error('Elements instance has not been initialized');
      }
    }
    return this.paymentElement;
  }

  // `keep` is the address of an element being rebuilt (BUG-2); otherwise the account's is the default.
  async createAddressElement(keep?: StripeAddressElementOptions['defaultValues']) {
    if (!this.addressElement) {
      const elements = await this.initializeElements();
      if (elements) {
        const options: StripeAddressElementOptions = {
          mode: 'shipping',
          defaultValues: keep ?? this.addressDefaultsFromAccount()
        };
        this.addressElement = elements.create('address', options);
      } else {
        throw new Error('Elements instance has not been loaded');
      }
    }
    return this.addressElement;
  }

  private addressDefaultsFromAccount(): StripeAddressElementOptions['defaultValues'] {
    const user = this.accountService.currentUser();
    const defaultValues: StripeAddressElementOptions['defaultValues'] = {};

    // Never concatenate directly — a user with no first/last name would yield the
    // literal string "null null", which then gets saved onto the order (BUG-13).
    const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ');
    if (fullName) {
      defaultValues.name = fullName;
    }

    if (user?.address) {
      defaultValues.address = {
        line1: user.address.line1,
        line2: user.address.line2,
        city: user.address.city,
        state: user.address.state,
        country: user.address.country,
        postal_code: user.address.postalCode
      };
    }

    return defaultValues;
  }

  async createToken() {
    const stripe = await this.getStripeInstance();
    const elements = await this.initializeElements();
    const result = await elements.submit();

    if (result.error) throw new Error(result.error.message);
    if (stripe) return await stripe.createConfirmationToken({ elements });
    else throw new Error('Stripe not available');
  }

  async confirmPayment(confirmationToken: ConfirmationToken) {
    const stripe = await this.getStripeInstance();
    const clientSecret = this.cartService.cart()?.clientSecret;

    if (stripe && clientSecret)
      return await stripe.confirmPayment({
        clientSecret: clientSecret,
        confirmParams: {
          confirmation_token: confirmationToken.id
        },
        redirect: 'if_required'
      });
    else {
      throw new Error('Unable to load Stripe');
    }
  }

  createOrUpdatePaymentIntent() {
    if (!this.cartService.cart()) throw new Error('Problem with cart');

    // Always store the response: the clientSecret changes when the server replaced a spent intent,
    // and confirmPayment() reads it off this signal.
    // Stored locally, not POSTed back -- the server already saved this cart. (BUG-2)
    return this.http.post<Cart>(this.baseUrl + 'payments', {}).pipe(
      tap((cart) => this.cartService.setLocalCart(cart)),
      // 409 = a price change or a retired coupon (BUG-19).
      // Recovered here because every caller funnels through this method.
      catchError((err: HttpErrorResponse) => {
        if (err.status === 409) this.checkoutConflict.recoverStaleCart();
        return throwError(() => err);
      })
    );
  }

  // Destroys the mounted elements too, so a rebuild can mount fresh ones into the same containers.
  disposeElements() {
    this.addressElement?.destroy();
    this.paymentElement?.destroy();
    this.elements = undefined;
    this.addressElement = undefined;
    this.paymentElement = undefined;
  }
}

// firstValueFrom
// converts an Observable to a Promise by waiting for the Observable to emit
// its first value, then resolving the Promise with that value.
