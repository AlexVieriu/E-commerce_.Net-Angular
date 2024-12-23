import { Component, inject } from '@angular/core';
import { ShopService } from '../../../core/services/shop.service';
import { MatDivider } from '@angular/material/divider';
import { MatListOption, MatSelectionList } from '@angular/material/list';
import { MatButton } from '@angular/material/button';

@Component({
  selector: 'app-filter-dialog',
  standalone: true,
  imports: [MatDivider, MatSelectionList, MatListOption, MatButton],
  templateUrl: './filter-dialog.component.html',
  styleUrl: './filter-dialog.component.scss'
})
export class FilterDialogComponent {
  shopService = inject(ShopService);
}
