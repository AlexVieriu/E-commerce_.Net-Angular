import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment.development';
import { HttpClient } from '@angular/common/http';
import { Cart } from '../../shared/models/cart';
import { CartItem } from '../../shared/models/cartItem';
import { Product } from '../../shared/models/products';

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

  addItemToCart(item: CartItem | Product, quantity = 1) {
    const cart = this.cart() ?? this.createCart();

    if (this.isProduct(item)) {
      item = this.mapProductToCartItem(item);
    }
    cart.items = this.addOrUpdateItem(cart.items, item, quantity);
    this.setCartAsync(cart);
  }


  // private methods
  private addOrUpdateItem(items: CartItem[], item: CartItem, quantity: number): CartItem[] {
    const idex = items.findIndex(i => i.productId === item.productId);
    if (idex === -1) { // not found
      item.quantity = quantity;
      items.push(item);
    } else {
      items[idex].quantity += quantity;
    }
    return items;
  }

  private createCart(): Cart {
    const cart = new Cart();
    localStorage.setItem('cart_id', cart.id);
    return cart;
  }

  // type guard
  private isProduct(item: CartItem | Product): item is Product {
    return (item as Product).id !== undefined;
  }

  private mapProductToCartItem(product: Product): CartItem {
    return {
      productId: product.id,
      productName: product.name,
      pictureUrl: product.pictureUrl,
      quantity: 0,
      brand: product.brand,
      type: product.type,
      price: product.price
    };
  }
}
