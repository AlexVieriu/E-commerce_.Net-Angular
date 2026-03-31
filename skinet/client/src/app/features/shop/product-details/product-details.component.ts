import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { CurrencyPipe } from '@angular/common';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatDivider } from '@angular/material/divider';
import { CartService } from '../../../core/services/cart.service';
import { FormsModule } from '@angular/forms';
import { Product } from '../../../shared/models/products';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-product-details',
  imports: [
    CurrencyPipe,
    MatButton,
    MatIcon,
    MatFormField,
    MatInput,
    MatLabel,
    MatDivider,
    FormsModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-details.component.html',
  styleUrl: './product-details.component.scss'
})
export class ProductDetailsComponent {
  private cartService = inject(CartService);

  id = input.required<string>();
  product = httpResource<Product>(() => environment.apiUrl + 'products/' + this.id());
  quantityInCart = computed(() =>
    this.cartService.cart()?.items.find(x => x.productId === this.product.value()?.id)?.quantity ?? 0
  );
  quantity = linkedSignal(() => this.quantityInCart() || 1);

  updateCart() {
    if (!this.product.value()) return;
    if (this.quantity() > this.quantityInCart()) {
      const itemsToAdd = this.quantity() - this.quantityInCart();
      this.cartService.addItemToCart(this.product.value()!, itemsToAdd);
    } else {
      const itemsToRemove = this.quantityInCart() - this.quantity();
      this.cartService.removeItemFromCart(this.product.value()!.id, itemsToRemove);
    }
  }

  getButtonText() {
    return this.quantityInCart() > 0 ? 'Update cart' : 'Add to cart';
  }
}