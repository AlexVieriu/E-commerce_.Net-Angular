import { Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { SignalrService } from '../../../core/services/signalr.service';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CurrencyPipe, DatePipe, NgIf } from '@angular/common';
import { AddressPipe } from '../../../shared/pipes/address.pipe';
import { CardPipe } from '../../../shared/pipes/card.pipe';

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
export class CheckoutSuccessComponent {
  signalrService = inject(SignalrService);

}
