import { Page, expect } from '@playwright/test';

export class OrdersPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/orders');
  }

  async expectAnyOrderVisible() {
    // Retries until a row is visible; fails after expect's default 5s.
    await expect(this.page.locator('tbody tr').first()).toBeVisible();
  }
}
