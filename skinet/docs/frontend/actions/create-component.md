# How to: Create a Component

## When to use this

Adding a new standalone component inside a feature or shared folder.

## Steps

1. `ng g c features/<name>/<component-name> --skip-tests`
2. Add `changeDetection: ChangeDetectionStrategy.OnPush` to the decorator.
3. Declare all needed imports in the component's `imports: []` array.
4. If reusable across features, put in `shared/components/` instead.

## Example

```typescript
@Component({
  selector: 'app-product-item',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-item.component.html',
})
export class ProductItemComponent {
  product = input.required<Product>();
}
```

## Pitfalls

- Omitting `ChangeDetectionStrategy.OnPush` — silent performance regression in zoneless mode.
- Importing a module (e.g., `CommonModule`) instead of the specific directive/pipe needed.
