import { computed, inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Cart } from '../../shared/models/cart';
import { CartItem } from '../../shared/models/cartItem';
import { Product } from '../../shared/models/products';
import { DeliveryMethod } from '../../shared/models/deliveryMethod';
import { Coupon } from '../../shared/models/coupon';
import { EMPTY, firstValueFrom, map, switchMap, tap } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class CartService {
  private readonly baseUrl = environment.apiUrl;
  private http = inject(HttpClient);
  private _cart = signal<Cart | null>(null);
  readonly cart = this._cart.asReadonly();

  itemCount = computed(() => {
    return this.cart()?.items.reduce((sum, item) => sum + item.quantity, 0);
  });

  private _selectedDelivery = signal<DeliveryMethod | null>(null);
  readonly selectedDelivery = this._selectedDelivery.asReadonly();

  totals = computed(() => {
    const cart = this.cart();
    const delivery = this.selectedDelivery();

    if (!cart) return null;
    const subtotal = cart.items.reduce((sum, item) => sum + item.price * item.quantity, 0);

    let discountValue = 0;

    if (cart.coupon) {
      if (cart.coupon.amountOff) {
        discountValue = cart.coupon.amountOff;
      } else if (cart.coupon.percentOff) {
        discountValue = subtotal * (cart.coupon.percentOff / 100);
      }
    }

    const shipping = delivery ? delivery.price : 0;

    return {
      subtotal,
      shipping,
      discount: discountValue,
      total: subtotal + shipping - discountValue
    };
  });

  getCartAsync() {
    return this.http.get<Cart>(this.baseUrl + 'cart').pipe(
      map((cart) => {
        this._cart.set(cart);
        return cart;
      })
    );
  }

  setCart(cart: Cart) {
    return this.http.post<Cart>(this.baseUrl + 'cart', cart).pipe(
      tap((response) => {
        this._cart.set(response);
      })
    );
  }

  async removeItemFromCart(productId: number, quantity = 1) {
    const cart = this.cart();
    if (!cart) return;

    const index = cart.items.findIndex((i) => i.productId === productId);
    if (index === -1) return;

    const updatedItems = [...cart.items];

    if (updatedItems[index].quantity > quantity) {
      updatedItems[index] = {
        ...updatedItems[index],
        quantity: updatedItems[index].quantity - quantity
      };
    } else {
      updatedItems.splice(index, 1);
    }

    if (updatedItems.length === 0) {
      this.deleteCart();
    } else {
      await this.saveCart({ ...cart, items: updatedItems });
    }
  }

  deleteCart() {
    this.http.delete(this.baseUrl + 'cart').subscribe({
      next: () => this._cart.set(null),
      error: () => this._cart.set(null)
    });
  }

  async addItemToCart(item: CartItem | Product, quantity = 1) {
    const cart = this.cart() ?? this.createCart();

    if (this.isProduct(item)) item = this.mapProductToCartItem(item);

    // A new cart object: the signal keeps the saved cart until the server accepts this one.
    await this.saveCart({ ...cart, items: this.addOrUpdateItem(cart.items, item, quantity) });
  }

  setSelectedDelivery(method: DeliveryMethod | null) {
    this._selectedDelivery.set(method);
  }

  // Stores a cart the server already saved, without POSTing it back (BUG-2).
  setLocalCart(cart: Cart) {
    this._cart.set(cart);
  }

  // Drops the cart from memory only — the server keeps it for the user's next login (BUG-18).
  clearLocalCart() {
    this._cart.set(null);
    this._selectedDelivery.set(null);
  }

  applyDiscount(code: string) {
    return this.http.get<Coupon>(this.baseUrl + 'coupons/' + code).pipe(
      switchMap((coupon) => {
        const cart = this.cart();
        if (!cart) return EMPTY;
        cart.coupon = coupon;
        return this.setCart(cart);
      })
    );
  }

  removeCoupon() {
    const cart = this.cart();
    if (!cart) return EMPTY;
    cart.coupon = undefined;
    return this.setCart(cart);
  }

  // private methods

  // errorInterceptor already reported the error; swallowed so a (click) caller can't leak a rejection.
  private async saveCart(cart: Cart) {
    try {
      await firstValueFrom(this.setCart(cart));
    } catch {
      // The cart signal still holds the last saved cart.
    }
  }

  // Returns new arrays and items, so a failed save never changes the cart in the signal.
  private addOrUpdateItem(items: CartItem[], item: CartItem, quantity: number): CartItem[] {
    const index = items.findIndex((i) => i.productId === item.productId);
    if (index === -1) return [...items, { ...item, quantity }];

    return items.map((existing, i) =>
      i === index ? { ...existing, quantity: existing.quantity + quantity } : existing
    );
  }

  private createCart(): Cart {
    return new Cart();
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
