import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIcon } from '@angular/material/icon';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Pagination } from '../../../shared/models/pagination';
import { Product } from '../../../shared/models/products';
import { ShopParams } from '../../../shared/models/shopParams';
import { ShopService } from '../../../core/services/shop.service';

@Component({
  selector: 'app-admin-products',
  imports: [
    CurrencyPipe,
    FormsModule,
    RouterLink,
    MatButton,
    MatFormFieldModule,
    MatInputModule,
    MatIcon,
    MatIconButton,
    MatPaginatorModule,
    MatProgressSpinner,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-products.component.html'
})
export class AdminProductsComponent {
  displayedColumns: string[] = ['image', 'name', 'brand', 'type', 'price', 'stock', 'action'];
  sortOptions = [
    { name: 'Name', value: 'name' },
    { name: 'Price: Low-High', value: 'priceAsc' },
    { name: 'Price: High-Low', value: 'priceDesc' }
  ];

  private shopService = inject(ShopService);
  // Reuse the shop's cached brand/type endpoints (root singletons) — no duplicate fetch.
  brands = this.shopService.brands;
  types = this.shopService.types;

  // ShopParams maps 1:1 to the backend ProductSpecParams — same endpoint, same contract as the shop.
  productParams = signal(new ShopParams());
  searchInput = signal('');

  products = httpResource<Pagination<Product>>(() => {
    const p = this.productParams();
    // Wire param is `pageIndex` (matches PagingParams.PageIndex); the model calls it pageNumber (BUG-12).
    const params: Record<string, string | number> = {
      pageIndex: p.pageNumber,
      pageSize: p.pageSize,
      sort: p.sort
    };

    if (p.search) params['search'] = p.search;
    if (p.brands.length > 0) params['brands'] = p.brands.join(',');
    if (p.types.length > 0) params['types'] = p.types.join(',');

    return { url: this.shopService.baseUrl + 'products', params };
  });

  onSearchChange() {
    this.productParams.update((p) => ({ ...p, search: this.searchInput(), pageNumber: 1 }));
  }

  onBrandsChange(event: MatSelectChange) {
    this.productParams.update((p) => ({ ...p, brands: event.value, pageNumber: 1 }));
  }

  onTypesChange(event: MatSelectChange) {
    this.productParams.update((p) => ({ ...p, types: event.value, pageNumber: 1 }));
  }

  onSortChange(event: MatSelectChange) {
    this.productParams.update((p) => ({ ...p, sort: event.value, pageNumber: 1 }));
  }

  onPageChanged(event: PageEvent) {
    this.productParams.update((p) => ({
      ...p,
      pageNumber: event.pageIndex + 1,
      pageSize: event.pageSize
    }));
  }
}
