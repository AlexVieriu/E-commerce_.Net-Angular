import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ShopService } from '../../../core/services/shop.service';
import { MatDivider } from '@angular/material/divider';
import { MatListOption, MatSelectionList } from '@angular/material/list';
import { MatButton } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-filters-dialog',
  imports: [MatDivider, MatSelectionList, MatListOption, MatButton, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filter-dialog.component.html',
  styleUrl: './filter-dialog.component.scss'
})
export class FiltersDialogComponent {
  shopService = inject(ShopService);
  private dialogRef = inject(MatDialogRef<FiltersDialogComponent>);
  data = inject(MAT_DIALOG_DATA);

  selectedBrands = signal<string[]>(this.data.selectedBrands);
  selectedTypes = signal<string[]>(this.data.selectedTypes);

  applyFilters() {
    this.dialogRef.close({
      selectedBrands: this.selectedBrands(),
      selectedTypes: this.selectedTypes()
    });
  }
}
