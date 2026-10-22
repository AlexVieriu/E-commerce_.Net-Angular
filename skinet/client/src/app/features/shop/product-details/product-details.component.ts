import { CartService } from '../../../core/services/cart.service';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CurrencyPipe, NgOptimizedImage } from '@angular/common';
import { environment } from '../../../../environments/environment';
import { httpResource } from '@angular/common/http';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatDivider } from '@angular/material/divider';
import { MatIcon } from '@angular/material/icon';
import { Product } from '../../../shared/models/products';

@Component({
  selector: 'app-product-details',
  imports: [CurrencyPipe, MatButton, MatIconButton, MatIcon, MatDivider, NgOptimizedImage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-details.component.html',
  styleUrl: './product-details.component.scss'
})
export class ProductDetailsComponent {
  private cartService = inject(CartService);

  id = input.required<string>();
  product = httpResource<Product>(() => environment.apiUrl + 'products/' + this.id());
  quantityInCart = computed(() => {
    const productId = this.product.value()?.id;
    const cartItem = this.cartService.cart()?.items.find((item) => item.productId === productId);
    return cartItem?.quantity ?? 0;
  });

  // The button only adds one; editing is a separate control (BUG-14).
  addOne() {
    const product = this.product.value();
    if (!product) return;
    this.cartService.addItemToCart(product);
  }

  // At quantity one, the service removes the cart item and the stepper hides.
  removeOne() {
    const product = this.product.value();
    if (!product) return;
    this.cartService.removeItemFromCart(product.id);
  }
}
