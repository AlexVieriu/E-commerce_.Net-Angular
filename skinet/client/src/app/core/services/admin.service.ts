import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Order } from '../../shared/models/order/order';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  baseUrl = environment.apiUrl;
  private http = inject(HttpClient);

  refundOrder(id: number) {
    return this.post(`admin/orders/refund/${id}`);
  }

  syncPaymentStatus(id: number) {
    return this.post(`admin/orders/${id}/sync-payment-status`);
  }

  private post(path: string) {
    return this.http.post<Order>(this.baseUrl + path, {});
  }
}
