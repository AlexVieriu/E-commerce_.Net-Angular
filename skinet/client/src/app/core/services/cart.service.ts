import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment.development';
import { HttpClient } from '@angular/common/http';
import { Cart } from '../../shared/models/cart';

@Injectable({
  providedIn: 'root'
})
export class CartService {
  baseUrl = environment.apiUrl;
  private http = inject(HttpClient);
  cart = signal<Cart | null>(null);

  getCartAsync() {
    return this.http.get<Cart>(this.baseUrl + 'cart?id=').subscribe({
      // It takes the cart data received from the API and updates the 
      // local cart signal by calling this.cart.set(cart).
      next: cart => this.cart.set(cart),
    });
  }

  setCartAsync(cart: Cart) {
    return this.http.post<Cart>(this.baseUrl + 'cart', cart).subscribe({
      next: cart => this.cart.set(cart),
    });
  }
}
