import {
  ApplicationConfig,
  provideZonelessChangeDetection,
  inject,
  provideAppInitializer
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { loadingInterceptor } from './core/interceptors/loading.interceptor';
import { InitService } from './core/services/init.service';
import { lastValueFrom } from 'rxjs';
import { authInterceptor } from './core/interceptors/auth.interceptor';

// Removes the #initial-splash screen (index.html) once init settles, success or failure.
function initializeApp() {
  const initService = inject(InitService);
  const result = lastValueFrom(initService.init()).finally(() => {
    const splash = document.getElementById('initial-splash');
    if (splash) {
      splash.remove();
    }
  });

  return result;
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding()),
    provideAnimationsAsync(),
    provideHttpClient(
      withXhr(),
      withInterceptors([errorInterceptor, loadingInterceptor, authInterceptor])
    ),
    // Bootstrap waits for this promise, so routes activate with the user and cart loaded.
    provideAppInitializer(initializeApp)
  ]
};
