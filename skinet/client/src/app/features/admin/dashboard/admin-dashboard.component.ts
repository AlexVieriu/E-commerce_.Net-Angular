import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { Dashboard } from '../../../shared/models/dashboard/dashboard';
import { MatCardModule } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatIconButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-admin-dashboard',
  imports: [
    CurrencyPipe,
    DatePipe,
    MatCardModule,
    MatIcon,
    MatIconButton,
    MatProgressSpinner,
    MatTableModule,
    MatTooltipModule,
    RouterLink
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-dashboard.component.html'
})
export class AdminDashboardComponent {
  recentOrderColumns: string[] = ['id', 'buyerEmail', 'orderDate', 'total', 'status'];
  lowStockColumns: string[] = ['name', 'quantityInStock'];

  // One call fills the whole page — 5 scalars + 2 preview lists (T27).
  dashboard = httpResource<Dashboard>(() => environment.apiUrl + 'admin/dashboard');

  // How many rows the Low-stock table actually shows (max 15).
  // If lowStockCount is bigger, the header says "showing 15 of 30" so the 15 rows aren't read as all of them.
  lowStockShown = computed(() => this.dashboard.value()?.lowStockProducts.length ?? 0);
}
