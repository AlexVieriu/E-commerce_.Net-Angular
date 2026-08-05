import { Page, expect } from '@playwright/test';

export class OrdersPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/orders');
  }

  async expectAnyOrderVisible() {
    // Keeps checking until the table has at least one visible row -- fails after 5s if it never does.
    await expect(this.page.locator('tbody tr').first()).toBeVisible();
  }
}
