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

  getProducts() {
    return this.http.get<Pagination<Product>>(this.baseUrl + 'products')
  }
}
