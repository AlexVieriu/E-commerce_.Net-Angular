import { Component, inject, OnDestroy } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SignalrService } from '../../../core/services/signalr.service';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CurrencyPipe, DatePipe, NgIf } from '@angular/common';
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
    NgIf],
  templateUrl: './checkout-success.component.html',
  styleUrl: './checkout-success.component.scss'
})
export class CheckoutSuccessComponent implements OnDestroy {
  signalrService = inject(SignalrService);
  private orderService = inject(OrderService);

  ngOnDestroy(): void {
    // once they left out the checkout success page, we set the flag to false and redirect someone else
    this.orderService.orderComplete = false;
    this.signalrService.orderSignal.set(null);
  }
}
