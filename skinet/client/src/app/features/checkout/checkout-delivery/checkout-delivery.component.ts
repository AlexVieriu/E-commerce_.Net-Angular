import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CheckoutService } from '../../../core/services/checkout.service';
import { MatRadioModule } from '@angular/material/radio';
import { CurrencyPipe } from '@angular/common';
import { CartService } from '../../../core/services/cart.service';
import { DeliveryMethod } from '../../../shared/models/deliveryMethod';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-checkout-delivery',
  imports: [MatRadioModule, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout-delivery.component.html',
  styleUrl: './checkout-delivery.component.scss'
})
export class CheckoutDeliveryComponent {
  checkoutService = inject(CheckoutService);
  cartService = inject(CartService);
  deliveryComplete = output<boolean>();

  constructor() {
    this.checkoutService.getDeliveryMethods().pipe(takeUntilDestroyed()).subscribe({
      next: methods => {
        if (this.cartService.cart()?.deliveryMethodId) {
          const method = methods.find(m => m.id === this.cartService.cart()?.deliveryMethodId);
          if (method) {
            this.cartService.setSelectedDelivery(method);
            this.deliveryComplete.emit(true);
          }
        };
      }
    });
  }

  async updateDeliveryMethod(method: DeliveryMethod) {
    this.cartService.setSelectedDelivery(method);
    const cart = this.cartService.cart();
    if (cart) {
      cart.deliveryMethodId = method.id;
      await firstValueFrom(this.cartService.setCart(cart));
      this.deliveryComplete.emit(true); // affect parent property from child component
    }
  }
}
