import { AfterViewInit, Component, inject, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { Order } from '../../shared/models/order/order';
import { MatPaginator } from '@angular/material/paginator';
import { AdminService } from '../../core/services/admin.service';
import { OrderParams } from '../../shared/models/order/orderParams';

@Component({
  selector: 'app-admin',
  imports: [MatTableModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements AfterViewInit, OnInit {
  displayedColumns: string[] = ['id', 'buyerEmail', 'orderDate', 'status', 'action'];
  statusOptions = ['All', 'PaymentReceived', 'PaymentMismatch', 'Refunded', 'Pending'];
  dataSource = new MatTableDataSource<Order>([]);

  private adminService = inject(AdminService);
  orderParams = new OrderParams();
  totalItems = 0;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // comes before the component is initialized
  ngOnInit(): void {
    this.loadOrders();
  }

  // comes after the component is initialized
  ngAfterViewInit() {
    this.dataSource.paginator = this.paginator;
  }

  loadOrders() {
    this.adminService.getOrders(this.orderParams).subscribe({
      next: response => {
        if (response.data) {
          this.dataSource.data = response.data;
          this.totalItems = response.count;
        }
      }
    });
  }

  onPageChanged(event: any) {
    this.orderParams.pageNumber = event.pageIndex + 1;
    this.orderParams.pageSize = event.pageSize;
    this.loadOrders();
  }

  onFilterSelect(event: any) {
    this.orderParams.filter = event.value;
    this.orderParams.pageNumber = 1;
    this.loadOrders();
  }
}
