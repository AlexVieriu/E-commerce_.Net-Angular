import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 400) {
        alert(error.error.title || error.error)
      }
      if (error.status === 401) {
        router.navigate(error.error.title || error.error)
      }
      if (error.status === 404) {
        router.navigateByUrl('/not-found')
      }
      if (error.status === 500) {
        router.navigateByUrl('/server-error')
      }
      return throwError(() => error);
    })
  );
};
