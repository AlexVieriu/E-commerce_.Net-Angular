import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { MatCard, MatCardActions, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { CurrencyPipe, NgOptimizedImage } from '@angular/common';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { Product } from '../../../shared/models/products';
import { CartService } from '../../../core/services/cart.service';

@Component({
  selector: 'app-product-item',
  imports: [
    MatCard,
    MatCardContent,
    MatCardActions,
    MatIcon,
    CurrencyPipe,
    MatButton,
    RouterLink,
    NgOptimizedImage
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-item.component.html',
  styleUrl: './product-item.component.scss'
})
export class ProductItemComponent {
  product = input<Product>();

  // Only the first card is the LCP candidate, so the grid marks one and the rest stay lazy.
  priority = input(false);

  cartService = inject(CartService);
}
