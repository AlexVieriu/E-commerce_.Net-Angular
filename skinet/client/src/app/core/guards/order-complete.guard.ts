import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { OrderService } from '../services/order.service';

export const orderCompleteGuard: CanActivateFn = () => {
  const orderService = inject(OrderService);
  const router = inject(Router);

  return orderService.orderComplete ? true : router.createUrlTree(['/shop']);
};
