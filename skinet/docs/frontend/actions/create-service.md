# How to: Create a Service

## When to use this

Adding a new service with signal state and HTTP calls.

## Steps

1. `ng g s core/services/<name> --skip-tests`
2. `providedIn: 'root'` is the default — keep it.
3. Declare private signals for state, expose via `asReadonly()`.
4. Use `inject(HttpClient)` for HTTP — update signals in `tap()`.
5. Use `computed()` for derived values.

## Example

```typescript
@Injectable({ providedIn: 'root' })
export class ShopService {
  private http = inject(HttpClient);
  private _products = signal<Product[]>([]);
  readonly products = this._products.asReadonly();

  getProducts(params: ProductSpecParams) {
    return this.http.get<Pagination<Product>>('/api/products', { params }).pipe(
      tap(result => this._products.set(result.data))
    );
  }
}
```

## Pitfalls

- Making a signal public without `asReadonly()` — components can mutate it directly.
- Putting the service in a feature folder — services go in `core/services/` only.
- Using `BehaviorSubject` for state — use signals.
