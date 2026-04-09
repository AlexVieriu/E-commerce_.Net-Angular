import { AdminService } from '../../core/services/admin.service';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { DialogService } from '../../core/services/dialog.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatIconButton } from '@angular/material/button';
import { MatLabel, MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Order } from '../../shared/models/order/order';
import { OrderParams } from '../../shared/models/order/orderParams';
import { Pagination } from '../../shared/models/pagination';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';

@Component({
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
    MatTabsModule,
    MatTooltipModule,
    RouterLink
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent {
  displayedColumns: string[] = ['id', 'buyerEmail', 'orderDate', 'total', 'status', 'action'];
  statusOptions = ['All', 'PaymentReceived', 'PaymentMismatch', 'Refunded', 'Pending'];

  private adminService = inject(AdminService);
  private dialogService = inject(DialogService);

  orderParams = signal(new OrderParams());

  orders = httpResource<Pagination<Order>>(() => {
    const p = this.orderParams();
    const params: Record<string, string> = {
      pageNumber: p.pageNumber.toString(),
      pageSize: p.pageSize.toString(),
    };
    if (p.filter && p.filter !== 'All') params['status'] = p.filter;
    return { url: environment.apiUrl + 'admin/orders', params };
  });

  onPageChanged(event: PageEvent) {
    this.orderParams.update(p => ({ ...p, pageNumber: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  onFilterSelect(event: MatSelectChange) {
    this.orderParams.update(p => ({ ...p, filter: event.value, pageNumber: 1 }));
  }

  refundOrder(id: number) {
    this.adminService.refundOrder(id).subscribe({
      next: () => this.orders.reload()
    });
  }

  async openConfirmDialog(id: number) {
    const confirmed = await this.dialogService.confirm(
      'Confirm Refund', 'Are you sure you want to refund this order?');

    if (confirmed)
      this.refundOrder(id);

  }
}