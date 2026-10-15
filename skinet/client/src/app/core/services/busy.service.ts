import { computed, Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class BusyService {
  // A signal, not a plain field: the app is zoneless, so a template read is what tells
  // the bar to re-render.
  private _busyRequestCount = signal(0);
  readonly busyRequestCount = this._busyRequestCount.asReadonly();
  readonly loading = computed(() => this._busyRequestCount() > 0);

  busy() {
    this._busyRequestCount.update((count) => count + 1);
  }

  idle() {
    this._busyRequestCount.update((count) => Math.max(0, count - 1));
  }
}
