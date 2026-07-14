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

  // The card counts every low-stock product; the list is capped server-side at 10.
  // Say so in the header, or the table silently claims to be the complete set.
  lowStockShown = computed(() => this.dashboard.value()?.lowStockProducts.length ?? 0);
}
