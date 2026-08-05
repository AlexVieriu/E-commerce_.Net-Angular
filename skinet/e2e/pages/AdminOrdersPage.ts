import { Page } from '@playwright/test';

export class AdminOrdersPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/admin/orders');
  }

  // These CSS classes come from Angular Material's table/paginator components.
  get rows() {
    return this.page.locator('tr.mat-mdc-row');
  }

  // The text like "1 – 10 of 25" that Material's paginator shows.
  rangeLabel() {
    return this.page.locator('.mat-mdc-paginator-range-label').textContent();
  }

  async goToNextPage() {
    await this.page.getByRole('button', { name: 'Next page' }).click();
  }
}
