import { inject, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { DeliveryMethod } from '../../shared/models/deliveryMethod';
import { asyncScheduler, map, of, scheduled } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class CheckoutService {
  baseUrl = environment.apiUrl;
  deliveryMethods: DeliveryMethod[] = [];

  private http = inject(HttpClient);

  getDeliveryMethods() {
    if (this.deliveryMethods.length > 0)
      return scheduled([this.deliveryMethods], asyncScheduler);
    // return of(this.deliveryMethods);

    return this.http.get<DeliveryMethod[]>(this.baseUrl + 'payments/delivery-methods').pipe(
      map(methods => {
        this.deliveryMethods = methods.sort((a, b) => b.price - a.price);
        return methods;
      })
    )
  }
}

/*
The selector you specified (#address-element) applies to no DOM elements 
that are currently on the page. Make sure the element exists on the page before calling mount().
*/