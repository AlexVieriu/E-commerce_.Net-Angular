import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SignalrService } from '../../../core/services/signalr.service';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { AddressPipe } from '../../../shared/pipes/address.pipe';
import { CardPipe } from '../../../shared/pipes/card.pipe';
import { OrderService } from '../../../core/services/order.service';

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
    inject(DestroyRef).onDestroy(() => {
      this.orderService.orderComplete = false;
      this.signalrService.orderSignal.set(null);
    });
  }
}
