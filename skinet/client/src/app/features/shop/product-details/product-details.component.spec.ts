import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductDetailsComponent } from './product-details.component';
import { Product } from '../../../shared/models/products';
import { Cart } from '../../../shared/models/cart';
import { environment } from '../../../../environments/environment';

// BUG-14: the button disabled itself on the click that had just worked.
// Every case waits on whenStable(), not detectChanges(), so the real scheduler runs.
describe('ProductDetailsComponent add to cart', () => {
  let fixture: ComponentFixture<ProductDetailsComponent>;
  let httpTesting: HttpTestingController;

  const product: Product = {
    id: 1,
    name: 'Angular Speedster Board 2000',
    description: 'A board',
    price: 200,
    pictureUrl: '/images/board.png',
    type: 'Boards',
    brand: 'Angular',
    quantityInStock: 10
  };

  const cartWith = (quantity: number): Cart => ({
    items: [
      {
        productId: product.id,
        productName: product.name,
        price: product.price,
        quantity,
        pictureUrl: product.pictureUrl,
        brand: product.brand,
        type: product.type
      }
    ]
  });

  const addButton = () =>
    fixture.nativeElement.querySelector('button[mat-flat-button]') as HTMLButtonElement;

  // The stepper is one flex row, so the minus button's parent is the whole control.
  const stepper = (): HTMLElement | null =>
    fixture.nativeElement.querySelector('[aria-label="Remove one from cart"]')?.parentElement ??
    null;

  // The count sits between the two buttons, so the minus button's next sibling is it.
  const inCartQuantity = () =>
    fixture.nativeElement
      .querySelector('[aria-label="Remove one from cart"]')
      ?.nextElementSibling?.textContent?.trim();

  // The page only renders once the product resolves, so every case starts from a flushed GET.
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [ProductDetailsComponent],
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()]
    });

    fixture = TestBed.createComponent(ProductDetailsComponent);
    httpTesting = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('id', '1');

    // httpResource fetches from an effect, so the request only exists after a sync pass — and
    // whenStable() would wait forever on the very request we have not flushed yet.
    TestBed.tick();
    httpTesting.expectOne(environment.apiUrl + 'products/1').flush(product);
    await fixture.whenStable();
  });

  afterEach(() => httpTesting.verify());

  it('offers a stepper only once the item is in the cart', () => {
    expect(stepper()).toBeNull();
  });

  // The regression: the click used to satisfy the disabled rule it had just changed.
  it('leaves the button enabled after a click', async () => {
    addButton().click();
    httpTesting.expectOne(environment.apiUrl + 'cart').flush(cartWith(1));
    await fixture.whenStable();

    expect(addButton().disabled).toBe(false);
  });

  it('shows the cart quantity once the item is added', async () => {
    addButton().click();
    httpTesting.expectOne(environment.apiUrl + 'cart').flush(cartWith(1));
    await fixture.whenStable();

    expect(inCartQuantity()).toBe('1');
  });

  // A second click has to add a second unit — the whole point of the report.
  it('adds another unit on a second click', async () => {
    addButton().click();
    httpTesting.expectOne(environment.apiUrl + 'cart').flush(cartWith(1));
    await fixture.whenStable();

    addButton().click();
    httpTesting.expectOne(environment.apiUrl + 'cart').flush(cartWith(2));
    await fixture.whenStable();

    expect(inCartQuantity()).toBe('2');
  });

  // Removing the last unit deletes the cart, so the stepper goes too instead of showing 0.
  it('drops the stepper when the last unit is removed', async () => {
    addButton().click();
    httpTesting.expectOne(environment.apiUrl + 'cart').flush(cartWith(1));
    await fixture.whenStable();

    fixture.nativeElement.querySelector('[aria-label="Remove one from cart"]').click();
    httpTesting
      .expectOne({ method: 'DELETE', url: environment.apiUrl + 'cart' })
      .flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(stepper()).toBeNull();
    expect(addButton().disabled).toBe(false);
  });
});
