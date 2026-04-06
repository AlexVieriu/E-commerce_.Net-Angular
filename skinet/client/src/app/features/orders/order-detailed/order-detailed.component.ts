import { AccountService } from '../../../core/services/account.service';
import { Router } from '@angular/router';
import { AddressPipe } from "../../../shared/pipes/address.pipe";
import { CardPipe } from "../../../shared/pipes/card.pipe";
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { MatButton } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Order } from '../../../shared/models/order/order';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-order-detailed',
  imports: [MatCardModule, MatButton, DatePipe, CurrencyPipe, CardPipe, AddressPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-detailed.component.html',
  styleUrl: './order-detailed.component.scss'
})
export class OrderDetailedComponent {
  private accountService = inject(AccountService);
  private router = inject(Router);

  id = input.required<string>();
  order = httpResource<Order>(() =>
    this.accountService.isAdmin()
      ? environment.apiUrl + 'admin/orders/' + this.id()
      : environment.apiUrl + 'orders/' + this.id()
  );
  buttonText = computed(() => this.accountService.isAdmin() ? 'Return to Admin' : 'Return to Orders');

  onReturnClick() {
    this.accountService.isAdmin()
      ? this.router.navigateByUrl('/admin')
      : this.router.navigateByUrl('/orders');
  }
}
