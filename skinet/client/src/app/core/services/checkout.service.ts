import { inject, Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { DeliveryMethod } from '../../shared/models/deliveryMethod';
import { asyncScheduler, map, scheduled } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class CheckoutService {
  baseUrl = environment.apiUrl;
  deliveryMethods = signal<DeliveryMethod[]>([]);

  private http = inject(HttpClient);

  getDeliveryMethods() {
    if (this.deliveryMethods().length > 0)
      return scheduled([this.deliveryMethods()], asyncScheduler);

    return this.http.get<DeliveryMethod[]>(this.baseUrl + 'payments/delivery-methods').pipe(
      map(methods => {
        const sorted = methods.sort((a, b) => b.price - a.price);
        this.deliveryMethods.set(sorted);
        return sorted;
      })
    )
  }
}