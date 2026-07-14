import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { ProductItemComponent } from './product-item/product-item.component';
import { MatDialog } from '@angular/material/dialog';
import { FiltersDialogComponent } from './filter-dialog/filter-dialog.component';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuTrigger } from '@angular/material/menu';
import { MatListOption, MatSelectionList, MatSelectionListChange } from '@angular/material/list';
import { ShopParams } from '../../shared/models/shopParams';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { Pagination } from '../../shared/models/pagination';
import { FormsModule } from '@angular/forms';
import { Product } from '../../shared/models/products';
import { ShopService } from '../../core/services/shop.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-shop',
  imports: [
    ProductItemComponent,
    MatButton,
    MatIcon,
    MatMenu,
    MatSelectionList,
    MatListOption,
    MatMenuTrigger,
    MatPaginator,
    FormsModule,
    EmptyStateComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shop.component.html',
  styleUrl: './shop.component.scss'
})
export class ShopComponent {
  private shopService = inject(ShopService);
  private dialogService = inject(MatDialog);

  shopParams = signal(new ShopParams());
  searchInput = signal('');
  sortOptions = [
    { name: 'Alphabetical', value: 'name' },
    { name: 'Price: Low-High', value: 'priceAsc' },
    { name: 'Price: High-Low', value: 'priceDesc' }
  ];
  pageSizeOptions = [5, 10, 15, 20];

  products = httpResource<Pagination<Product>>(() => {
    const p = this.shopParams();
    const params: Record<string, string> = {
      pageSize: p.pageSize.toString(),
      pageIndex: p.pageNumber.toString(),
      sort: p.sort
    };
    if (p.search) params['search'] = p.search;
    if (p.brands.length > 0) params['brands'] = p.brands.join(',');
    if (p.types.length > 0) params['types'] = p.types.join(',');
    return { url: this.shopService.baseUrl + 'products', params };
  });

  resetFilters() {
    this.searchInput.set('');
    this.shopParams.set(new ShopParams());
  }

  onSearchChange() {
    this.shopParams.update((p) => ({ ...p, search: this.searchInput(), pageNumber: 1 }));
  }

  handlePageEvent(event: PageEvent) {
    this.shopParams.update((p) => ({
      ...p,
      pageNumber: event.pageIndex + 1,
      pageSize: event.pageSize
    }));
  }

  onSortChange(event: MatSelectionListChange) {
    const selectedOption = event.options[0];
    if (selectedOption) {
      this.shopParams.update((p) => ({ ...p, sort: selectedOption.value, pageNumber: 1 }));
    }
  }

  openFiltersDialog() {
    const dialogRef = this.dialogService.open(FiltersDialogComponent, {
      minWidth: '500px',
      data: {
        selectedBrands: this.shopParams().brands,
        selectedTypes: this.shopParams().types
      }
    });
    dialogRef.afterClosed().subscribe({
      next: (result) => {
        if (result) {
          this.shopParams.update((p) => ({
            ...p,
            brands: result.selectedBrands,
            types: result.selectedTypes,
            pageNumber: 1
          }));
        }
      }
    });
  }
}
