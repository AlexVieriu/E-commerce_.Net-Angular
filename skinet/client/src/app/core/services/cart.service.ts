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

  getCartAsync(id: string) {
    return this.http.get<Cart>(this.baseUrl + 'cart?id=' + id).pipe(
      map((cart) => {
        // map - allows to perform side effects and transform emitted value before passing it downstream
        this._cart.set(cart); // Update the `cart` signal with the fetched cart data
        return cart; // Pass the cart data downstream to subscribers
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
      await firstValueFrom(this.setCart({ ...cart, items: updatedItems }));
    }
  }

  deleteCart() {
    this.http.delete(this.baseUrl + 'cart?id=' + this.cart()?.id).subscribe({
      next: () => {
        localStorage.removeItem('cart_id');
        this._cart.set(null);
      },
      error: () => {
        localStorage.removeItem('cart_id');
        this._cart.set(null);
      }
    });
  }

  async addItemToCart(item: CartItem | Product, quantity = 1) {
    const cart = this.cart() ?? this.createCart();

    if (this.isProduct(item)) item = this.mapProductToCartItem(item);

    cart.items = this.addOrUpdateItem(cart.items, item, quantity);
    await firstValueFrom(this.setCart(cart));
  }

  setSelectedDelivery(method: DeliveryMethod | null) {
    this._selectedDelivery.set(method);
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
  private addOrUpdateItem(items: CartItem[], item: CartItem, quantity: number): CartItem[] {
    const idex = items.findIndex((i) => i.productId === item.productId);
    if (idex === -1) {
      // not found
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
