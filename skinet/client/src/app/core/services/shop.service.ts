import { httpResource, HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Pagination } from '../../shared/models/pagination';
import { ShopParams } from '../../shared/models/shopParams';
import { Product, ProductToCreate } from '../../shared/models/products';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ShopService {
  baseUrl = environment.apiUrl;
  private http = inject(HttpClient);
  brands = httpResource<string[]>(() => this.baseUrl + 'products/brands');
  types = httpResource<string[]>(() => this.baseUrl + 'products/types');

  getProducts(shopParams: ShopParams) {
    let params = new HttpParams();

    if (shopParams.brands.length > 0) {
      params = params.append('brands', shopParams.brands.join(','));
    }

    if (shopParams.types.length > 0) {
      params = params.append('types', shopParams.types.join(','));
    }

    if (shopParams.sort) {
      params = params.append('sort', shopParams.sort);
    }

    if (shopParams.search) {
      params = params.append('search', shopParams.search);
    }

    params = params.append('pageSize', shopParams.pageSize);
    params = params.append('pageIndex', shopParams.pageNumber);

    return this.http.get<Pagination<Product>>(this.baseUrl + 'products', { params });
  }

  getProduct(id: number) {
    return this.http.get<Product>(this.baseUrl + 'products/' + id);
  }

  createProduct(product: ProductToCreate) {
    return this.http.post<Product>(this.baseUrl + 'products', product);
  }

  updateProduct(id: number, product: ProductToCreate) {
    return this.http.put<Product>(this.baseUrl + 'products/' + id, product);
  }

  deleteProduct(id: number) {
    return this.http.delete<void>(this.baseUrl + 'products/' + id); // 204 No Content
  }
}
