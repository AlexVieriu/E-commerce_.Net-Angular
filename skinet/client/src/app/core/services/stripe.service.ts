import { inject, Injectable } from '@angular/core';
import { loadStripe, Stripe, StripeAddressElement, StripeAddressElementOptions, StripeElements } from '@stripe/stripe-js';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { CartService } from './cart.service';
import { Cart } from '../../shared/models/cart';
import { map } from 'rxjs/internal/operators/map';
import { firstValueFrom } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class StripeService {
  baseUrl = environment.apiUrl;
  private stripePromise: Promise<Stripe | null>
  private http = inject(HttpClient);
  private cartService = inject(CartService);
  private elements?: StripeElements;
  private addressElement?: StripeAddressElement;

  constructor() {
    this.stripePromise = loadStripe(environment.stripePublicKey);
  }

  getStripePromise() {
    return this.stripePromise;
  }

  createOrUpdatePaymentIntent() {
    const cart = this.cartService.cart();
    if (!cart)
      throw new Error("Cart is empty");

    return this.http.post<Cart>(this.baseUrl + 'payments/', cart?.id, {}).pipe(
      map(cart => {
        this.cartService.cart.set(cart);
        return cart;
      })
    );
  }

  async initializeStripeElements() {
    if (!this.elements) {
      const stripe = await this.getStripePromise();
      if (stripe) {
        const cart = await firstValueFrom(this.createOrUpdatePaymentIntent());
        this.elements = stripe.elements({ clientSecret: cart.clientSecret, appearance: { labels: 'floating' } });
      }
      else {
        throw new Error("Stripe is not initialized");
      }
    }
    return this.elements;
  }

  async createAddressElement() {
    if (!this.addressElement) {
      const elements = await this.initializeStripeElements();
      if (elements) {
        const options: StripeAddressElementOptions = {
          // when we use shipping, when it gets to payment option, they are ask if they wanna use 
          // the same address for shipping and billing          
          mode: 'shipping',
        }
        this.addressElement = elements.create('address', options);
      }
      else
        throw new Error("Stripe Elements is not initialized");
    }
    return this.addressElement
  }
}
