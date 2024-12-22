import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Pagination } from '../../shared/models/pagination';
import { Product } from '../../shared/models/products';

@Injectable({
  // when the app is start running is provided in the entire app
  providedIn: 'root'
})
export class ShopService {
  baseUrl = "https://localhost:7096/api/"
  // inject HTTP Client
  private http = inject(HttpClient);
  types: string[] = [];
  brands: string[] = [];

  getProducts() {
    return this.http.get<Pagination<Product>>(this.baseUrl + 'products?pageSize=20');
  }

  getBrands() {
    // condition for not executing at infinite
    if (this.brands.length > 0)
      return;

    return this.http.get<string[]>(this.baseUrl + 'products/brands').subscribe({
      next: response => this.brands = response,
      error: error => console.log(error)
    });
  }

  getTypes() {
    // condition for not executing at infinite
    if (this.types.length > 0)
      return;

    return this.http.get<string[]>(this.baseUrl + 'products/types').subscribe({
      next: response => this.types = response,
      error: error => console.log(error)
    });
  }
}
