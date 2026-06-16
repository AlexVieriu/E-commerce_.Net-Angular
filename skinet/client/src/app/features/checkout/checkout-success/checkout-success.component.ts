import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SignalrService } from '../../../core/services/signalr.service';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { AddressPipe } from '../../../shared/pipes/address.pipe';
import { CardPipe } from '../../../shared/pipes/card.pipe';
import { OrderService } from '../../../core/services/order.service';
import { firstValueFrom } from 'rxjs';

// Fallback wait before fetching the order directly if no SignalR push has arrived.
const SIGNALR_FALLBACK_MS = 5000;

@Component({
  selector: 'app-checkout-success',
  imports: [
    RouterLink,
    MatButton,
    MatProgressSpinnerModule,
    DatePipe,
    AddressPipe,
    CurrencyPipe,
    CardPipe,
],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout-success.component.html',
  styleUrl: './checkout-success.component.scss'
})
export class CheckoutSuccessComponent {
  signalrService = inject(SignalrService);
  private orderService = inject(OrderService);

  constructor() {
    // Primary path: the Stripe webhook pushes OrderCompleteNotification over SignalR,
    // which sets orderSignal. Fallback: if no push arrives within the timeout (webhook
    // delayed or notification lost), fetch the order by id so the user never sees an
    // endless spinner.
    const orderId = this.orderService.createdOrder?.id;
    const fallbackTimer = setTimeout(() => {
      if (this.signalrService.orderSignal() || !orderId) return;
      firstValueFrom(this.orderService.getOrderDetailed(orderId))
        .then(order => {
          if (!this.signalrService.orderSignal()) this.signalrService.orderSignal.set(order);
        })
        .catch(() => { /* leave spinner; nothing better to show */ });
    }, SIGNALR_FALLBACK_MS);

    inject(DestroyRef).onDestroy(() => {
      clearTimeout(fallbackTimer);
      this.orderService.orderComplete = false;
      this.orderService.createdOrder = null;
      this.signalrService.orderSignal.set(null);
    });
  }
}
