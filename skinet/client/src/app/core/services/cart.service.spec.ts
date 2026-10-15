import { describe, expect, it, beforeEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CartService } from './cart.service';
import { Cart } from '../../shared/models/cart';
import { CartItem } from '../../shared/models/cartItem';
import { environment } from '../../../environments/environment';

// errorInterceptor reacts to a failed save (snackbar or login redirect) and rethrows it.
// Templates call these async methods from (click) and ignore the promise, so a rejection would be uncaught.
describe('CartService — a rejected cart save', () => {
  let service: CartService;
  let httpTesting: HttpTestingController;

  const item = (productId: number, quantity: number): CartItem => ({
    productId,
    productName: 'Board ' + productId,
    pictureUrl: '',
    quantity,
    brand: 'Brand',
    type: 'Type',
    price: 10
  });

  const savedCart = (): Cart => ({ items: [item(1, 2)] });

  const fail401 = () =>
    httpTesting
      .expectOne(environment.apiUrl + 'cart')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(CartService);
    httpTesting = TestBed.inject(HttpTestingController);
    service.setLocalCart(savedCart());
  });

  it('addItemToCart resolves and leaves the cart as the server last saved it', async () => {
    const pending = service.addItemToCart(item(1, 1));
    fail401();

    await expect(pending).resolves.toBeUndefined();
    expect(service.cart()).toEqual(savedCart());
    httpTesting.verify();
  });

  it('addItemToCart does not add a new item locally when the save fails', async () => {
    const pending = service.addItemToCart(item(2, 1));
    fail401();

    await expect(pending).resolves.toBeUndefined();
    expect(service.cart()?.items).toEqual(savedCart().items);
  });

  it('removeItemFromCart resolves and keeps the item', async () => {
    const pending = service.removeItemFromCart(1, 1);
    fail401();

    await expect(pending).resolves.toBeUndefined();
    expect(service.cart()).toEqual(savedCart());
  });
});
