import { Page } from '@playwright/test';

export class AdminOrdersPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/admin/orders');
  }

  // Angular Material's table and paginator classes.
  get rows() {
    return this.page.locator('tr.mat-mdc-row');
  }

  // The paginator's "1 – 10 of 25" label.
  rangeLabel() {
    return this.page.locator('.mat-mdc-paginator-range-label').textContent();
  }

  async goToNextPage() {
    await this.page.getByRole('button', { name: 'Next page' }).click();
  }
}
