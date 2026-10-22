import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { FiltersDialogComponent } from './filter-dialog/filter-dialog.component';
import { FormsModule } from '@angular/forms';
import { httpResource } from '@angular/common/http';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatListOption, MatSelectionList, MatSelectionListChange } from '@angular/material/list';
import { MatMenu, MatMenuTrigger } from '@angular/material/menu';
import { MatPaginator, PageEvent } from '@angular/material/paginator';
import { Pagination } from '../../shared/models/pagination';
import { Product } from '../../shared/models/products';
import { ProductItemComponent } from './product-item/product-item.component';
import { ShopParams } from '../../shared/models/shopParams';
import { ShopService } from '../../core/services/shop.service';

@Component({
  selector: 'app-shop',
  imports: [
    EmptyStateComponent,
    FormsModule,
    MatButton,
    MatIcon,
    MatListOption,
    MatMenu,
    MatMenuTrigger,
    MatPaginator,
    MatSelectionList,
    ProductItemComponent
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
