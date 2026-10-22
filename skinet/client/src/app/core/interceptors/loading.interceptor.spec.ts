import { describe, expect, it, beforeEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { Subject } from 'rxjs';
import { loadingInterceptor } from './loading.interceptor';
import { BusyService } from '../services/busy.service';

// The count must return to 0 on every ending, not just a response.
// A stuck count is BUG-10's other half: the bar spins forever.
describe('loadingInterceptor counter', () => {
  let busyService: BusyService;

  const request = new HttpRequest('GET', '/api/products');

  const run = (next: HttpHandlerFn) =>
    TestBed.runInInjectionContext(() => loadingInterceptor(request, next));

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    busyService = TestBed.inject(BusyService);
  });

  it('does not count a request nobody subscribes to', () => {
    run(() => new Subject());

    expect(busyService.busyRequestCount()).toBe(0);
  });

  it('counts a request while it is in flight', () => {
    run(() => new Subject()).subscribe();

    expect(busyService.busyRequestCount()).toBe(1);
  });

  // A later interceptor throwing on the spot skipped the finalize before this was wrapped in defer.
  it('gives the count back when a later interceptor throws synchronously', () => {
    let failed = false;

    run(() => {
      throw new Error('a downstream interceptor blew up');
    }).subscribe({ error: () => (failed = true) });

    expect(failed).toBe(true);
    expect(busyService.busyRequestCount()).toBe(0);
  });

  it('gives the count back when the caller unsubscribes before the response', () => {
    const subscription = run(() => new Subject()).subscribe();

    subscription.unsubscribe();

    expect(busyService.busyRequestCount()).toBe(0);
  });
});
