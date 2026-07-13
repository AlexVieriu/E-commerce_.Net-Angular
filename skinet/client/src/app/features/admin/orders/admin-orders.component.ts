import { AdminService } from '../../../core/services/admin.service';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { DialogService } from '../../../core/services/dialog.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatIconButton } from '@angular/material/button';
import { MatLabel, MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable } from 'rxjs';
import { Order, OrderStatus } from '../../../shared/models/order/order';
import { OrderParams } from '../../../shared/models/order/orderParams';
import { Pagination } from '../../../shared/models/pagination';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-admin-orders',
  imports: [
    CurrencyPipe,
    DatePipe,
    MatFormFieldModule,
    MatIcon,
    MatIconButton,
    MatLabel,
    MatPaginatorModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
    RouterLink
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-orders.component.html',
  styleUrl: './admin-orders.component.scss'
})
export class AdminOrdersComponent {
  displayedColumns: string[] = ['id', 'buyerEmail', 'orderDate', 'total', 'status', 'action'];
  statusOptions: (OrderStatus | 'All')[] = ['All', 'PaymentReceived', 'PaymentMismatch', 'Refunded', 'Pending'];

  private adminService = inject(AdminService);
  private dialogService = inject(DialogService);

  orderParams = signal(new OrderParams());

  orders = httpResource<Pagination<Order>>(() => {
    const p = this.orderParams();
    const params: Record<string, string | number> = {
      pageNumber: p.pageNumber,
      pageSize: p.pageSize
    };

    if (p.filter && p.filter !== 'All')
      params['status'] = p.filter;

    return { url: environment.apiUrl + 'admin/orders', params };
  });

  onPageChanged(event: PageEvent) {
    this.orderParams.update(p => ({ ...p, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  onFilterSelect(event: MatSelectChange) {
    this.orderParams.update(p => ({ ...p, filter: event.value, pageNumber: 1 }));
  }

  async openConfirmDialog(id: number) {
    const confirmed = await this.dialogService.confirm(
      'Confirm Refund', 'Are you sure you want to refund this order?');

    if (confirmed)
      this.refundOrder(id);
  }

  refundOrder(id: number) {
    this.reloadAfter(this.adminService.refundOrder(id));
  }

  syncPaymentStatus(id: number) {
    this.reloadAfter(this.adminService.syncPaymentStatus(id));
  }

  private reloadAfter(action: Observable<Order>) {
    action.subscribe({ next: () => this.orders.reload() });
  }
}
