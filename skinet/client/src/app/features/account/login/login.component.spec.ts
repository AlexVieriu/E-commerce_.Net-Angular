import { afterEach, describe, expect, it, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';

// The component reads returnUrl in its constructor, so each case needs its own TestBed.
function createWith(returnUrl?: unknown) {
  TestBed.configureTestingModule({
    imports: [LoginComponent],
    providers: [
      provideZonelessChangeDetection(),
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      // Declared after provideRouter so it wins — the component only reads snapshot.queryParams.
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { queryParams: returnUrl === undefined ? {} : { returnUrl } } }
      }
    ]
  });

  return TestBed.createComponent(LoginComponent).componentInstance;
}

const TAB = String.fromCharCode(9);
const BACKSLASH = String.fromCharCode(92);

describe('LoginComponent returnUrl', () => {
  // Spies on console.warn survive the spec otherwise, so the next one sees this one's calls.
  afterEach(() => vi.restoreAllMocks());

  it('defaults to /shop when the query string carries none', () => {
    expect(createWith().returnUrl).toBe('/shop');
  });

  it('keeps a local path', () => {
    expect(createWith('/checkout').returnUrl).toBe('/checkout');
  });

  it('keeps a local path with a query string', () => {
    expect(createWith('/orders?page=2').returnUrl).toBe('/orders?page=2');
  });

  it('keeps a bare slash', () => {
    expect(createWith('/').returnUrl).toBe('/');
  });

  // Every shape below resolves to another host once a browser parses it.
  const offSite = [
    'https://evil.com',
    '//evil.com',
    '////evil.com',
    '/' + BACKSLASH + 'evil.com',
    '/' + TAB + '/evil.com',
    'javascript:alert(1)',
    'shop'
  ];

  offSite.forEach((value) => {
    it(`falls back to /shop for ${JSON.stringify(value)}`, () => {
      expect(createWith(value).returnUrl).toBe('/shop');
    });
  });

  // Angular turns a repeated ?returnUrl=a&returnUrl=b into an array (_router-chunk.mjs:551).
  it('falls back to /shop when the parameter is repeated', () => {
    expect(createWith(['/checkout', 'https://evil.com']).returnUrl).toBe('/shop');
  });

  it('warns when a value is rejected', () => {
    const warn = vi.spyOn(console, 'warn');

    createWith('https://evil.com');

    expect(warn).toHaveBeenCalled();
  });

  it('stays silent when the value is accepted', () => {
    const warn = vi.spyOn(console, 'warn');

    createWith('/checkout');

    expect(warn).not.toHaveBeenCalled();
  });
});
