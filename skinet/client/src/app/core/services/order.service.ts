import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { OrderToCreate } from '../../shared/models/order/orderToCreate';
import { Order } from '../../shared/models/order/order';

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  baseUrl = environment.apiUrl;
  private http = inject(HttpClient)

  createOrder(orderToCreate: OrderToCreate) {
    return this.http.post(this.baseUrl + 'order', orderToCreate);
  }

  getOrdersForUser() {
    return this.http.get<Order[]>(this.baseUrl + 'orders');
  }

  getOrderDetailed(id: number) {
    return this.http.get<Order>(this.baseUrl + 'order/' + id);
  }
}