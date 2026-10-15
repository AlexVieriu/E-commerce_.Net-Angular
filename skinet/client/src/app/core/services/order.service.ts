import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { OrderToCreate } from '../../shared/models/order/orderToCreate';
import { Order } from '../../shared/models/order/order';
import { CheckoutConflictService } from './checkout-conflict.service';

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  baseUrl = environment.apiUrl;
  private http = inject(HttpClient);
  private checkoutConflict = inject(CheckoutConflictService);
  orderComplete = false;
  // Order created during checkout (before payment confirm). Lets the success page
  // fall back to fetching it by id if the SignalR push is delayed or lost.
  createdOrder: Order | null = null;

  createOrder(orderToCreate: OrderToCreate) {
    return this.http.post(this.baseUrl + 'orders', orderToCreate).pipe(
      // 409 = the prices or the coupon moved while the customer was on checkout.
      catchError((err: HttpErrorResponse) => {
        if (err.status === 409) this.checkoutConflict.recoverStaleCart();
        return throwError(() => err);
      })
    );
  }

  getOrdersForUser() {
    return this.http.get<Order[]>(this.baseUrl + 'orders');
  }

  getOrderDetailed(id: number) {
    return this.http.get<Order>(this.baseUrl + 'orders/' + id);
  }
}
