import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Cart } from '../../shared/models/cart';
import { CartItem } from '../../shared/models/cartItem';
import { Product } from '../../shared/models/products';
import { map } from 'rxjs/internal/operators/map';
import { DeliveryMethod } from '../../shared/models/deliveryMethod';
import { Coupon } from '../../shared/models/coupon';
import { tap } from 'rxjs';

@Injectable({
  providedIn: 'root'
})

export class CartService {
  baseUrl = environment.apiUrl;
  private http = inject(HttpClient);
  cart = signal<Cart | null>(null);
  itemCount = computed(() => {
    return this.cart()?.items.reduce((sum, item) => sum + item.quantity, 0)
  })

  selectedDelivery = signal<DeliveryMethod | null>(null);
  couponCode = signal<Coupon | null>(null);

  totals = computed(() => {
    const cart = this.cart();
    const delivery = this.selectedDelivery();



    if (!cart) return null;
    const subtotal = cart.items.reduce((sum, item) => sum + item.price * item.quantity, 0);
    const shipping = delivery ? delivery.price : 0;
    const discount = this.couponCode()?.amountOff ?? 0;

    return {
      subtotal,
      shipping,
      discount,
      total: subtotal + shipping - discount
    }
  })

  getCartAsync(id: string) {
    return this.http.get<Cart>(this.baseUrl + 'cart?id=' + id).pipe(
      map(cart => { // map - allows to perform side effects and transform emitted value before passing it downstream
        this.cart.set(cart); // Update the `cart` signal with the fetched cart data
        return cart; // Pass the cart data downstream to subscribers
      })
    )
  }

  setCartAsync(cart: Cart) { // save cart in redis DB
    return this.http.post<Cart>(this.baseUrl + 'cart', cart).pipe(
      tap(cart => this.cart.set(cart))
    )
  }

  removeItemFromCart(productId: number, quantity = 1) {
    const cart = this.cart();
    if (!cart) return;

    const index = cart.items.findIndex(i => i.productId === productId);
    if (index != -1) {
      if (cart.items[index].quantity > quantity) {
        cart.items[index].quantity -= quantity;
      }
      else {
        cart.items.splice(index, 1);
      }
      if (cart.items.length === 0) {
        this.deleteCart();
      }
      else {
        this.setCartAsync(cart)
      }
    }
  }

  deleteCart() {
    this.http.delete(this.baseUrl + 'cart?id=' + this.cart()?.id).subscribe({
      next: () => {
        localStorage.removeItem('cart_id');
        this.cart.set(null);
      }
    })
  }

  addItemToCart(item: CartItem | Product, quantity = 1) {
    const cart = this.cart() ?? this.createCart();

    if (this.isProduct(item)) {
      item = this.mapProductToCartItem(item);
    }
    cart.items = this.addOrUpdateItem(cart.items, item, quantity);
    this.setCartAsync(cart);
  }

  applyDiscount(code: string) {
    return this.http.get<Coupon>(this.baseUrl + 'coupons/' + code);
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
