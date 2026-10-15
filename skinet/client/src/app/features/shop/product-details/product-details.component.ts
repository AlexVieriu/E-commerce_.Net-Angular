import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { CurrencyPipe, NgOptimizedImage } from '@angular/common';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatDivider } from '@angular/material/divider';
import { CartService } from '../../../core/services/cart.service';
import { Product } from '../../../shared/models/products';
import { environment } from '../../../../environments/environment';

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
  quantityInCart = computed(
    () =>
      this.cartService.cart()?.items.find((x) => x.productId === this.product.value()?.id)
        ?.quantity ?? 0
  );

  // Adding and editing are separate intents (BUG-14), so the button only ever adds one.
  addOne() {
    const product = this.product.value();
    if (!product) return;
    this.cartService.addItemToCart(product);
  }

  // At a quantity of one the service drops the line, and the stepper disappears with it.
  removeOne() {
    const product = this.product.value();
    if (!product) return;
    this.cartService.removeItemFromCart(product.id);
  }
}
