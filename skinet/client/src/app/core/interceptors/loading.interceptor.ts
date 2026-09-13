import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { defer, delay, finalize, identity } from 'rxjs';
import { BusyService } from '../services/busy.service';
import { environment } from '../../../environments/environment';

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const busyService = inject(BusyService);

  // Counted inside defer, so a synchronous throw downstream becomes an error
  // notification and finalize still runs.
  return defer(() => {
    busyService.busy();
    return next(req);
  }).pipe(
    environment.production ? identity : delay(500),
    finalize(() => busyService.idle()) // after the request is completed
  );
};
