import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButton } from '@angular/material/button';
import { MatCard } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { ShopService } from '../../../core/services/shop.service';
import { SnackbarService } from '../../../core/services/snackbar.service';
import { ValidationError } from '../../../shared/models/validationError';

@Component({
  selector: 'app-product-form',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButton,
    MatCard,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-form.component.html'
})
export class ProductFormComponent {
  private fb = inject(FormBuilder);
  private shopService = inject(ShopService);
  private snack = inject(SnackbarService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // :id present → edit an existing product; absent → create. One component, both modes.
  private readonly productId = this.route.snapshot.paramMap.get('id');
  protected readonly isEdit = this.productId !== null;

  // Reuse the shop's cached brand/type endpoints for the autocomplete options.
  private brands = this.shopService.brands;
  private types = this.shopService.types;
  protected validationErrors = signal<string[] | undefined>(undefined);
  // True while a create/update request is in flight — disables the submit button so rapid clicks can't POST twice.
  protected isSubmitting = signal(false);

  // Validators mirror CreateProductDtoValidator; the server stays the source of truth.
  protected productForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: ['', Validators.required],
    price: [0, [Validators.required, Validators.min(0.01)]],
    pictureUrl: ['', Validators.required],
    brand: ['', Validators.required],
    type: ['', Validators.required],
    quantityInStock: [1, [Validators.required, Validators.min(1)]]
  });

  // Signals off the form's value stream — so OnPush re-renders the preview + filtered options per keystroke.
  private brandTerm = toSignal(this.productForm.controls.brand.valueChanges, { initialValue: '' });
  private typeTerm = toSignal(this.productForm.controls.type.valueChanges, { initialValue: '' });
  protected picturePreview = toSignal(this.productForm.controls.pictureUrl.valueChanges, {
    initialValue: ''
  });

  protected filteredBrands = computed(() =>
    (this.brands.value() ?? []).filter((b) =>
      b.toLowerCase().includes((this.brandTerm() ?? '').toLowerCase())
    )
  );
  protected filteredTypes = computed(() =>
    (this.types.value() ?? []).filter((t) =>
      t.toLowerCase().includes((this.typeTerm() ?? '').toLowerCase())
    )
  );

  constructor() {
    // Edit mode: prefill from the existing product (patchValue ignores the extra `id`).
    if (this.productId)
      this.shopService.getProduct(+this.productId).subscribe((p) => this.productForm.patchValue(p));
  }

  protected onSubmit() {
    this.isSubmitting.set(true);
    const values = this.productForm.getRawValue();
    const request = this.productId
      ? this.shopService.updateProduct(+this.productId, values)
      : this.shopService.createProduct(values);

    request.subscribe({
      next: () => {
        this.snack.success(`Product ${this.isEdit ? 'updated' : 'created'}`);
        this.router.navigateByUrl('/admin/products');
      },
      error: (err: unknown) => {
        // Reset so the admin can fix the input and resubmit; on success we navigate away instead.
        this.isSubmitting.set(false);
        if (err instanceof ValidationError) this.validationErrors.set(err.errors);
      }
    });
  }
}
