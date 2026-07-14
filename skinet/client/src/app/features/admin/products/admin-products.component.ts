import { ChangeDetectionStrategy, Component } from '@angular/core';

// Placeholder — T29 (list) + T30 (create/edit form) build this out.
@Component({
  selector: 'app-admin-products',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="text-2xl font-semibold">Products</h2>
    <p class="text-gray-500 mt-2">Product table and form land here in T29 / T30.</p>
  `
})
export class AdminProductsComponent {}
