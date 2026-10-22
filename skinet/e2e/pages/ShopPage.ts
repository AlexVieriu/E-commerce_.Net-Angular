import { Page, expect } from '@playwright/test';

export class ShopPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/shop');
  }

  async addFirstProductToCart() {
    await this.page
      .locator('.product-card')
      .first()
      .getByRole('button', { name: /add to cart/i })
      .click();

    // Navigating away too fast can cancel the add-to-cart request.
    // So wait for the cart badge to show "1" — proof the item was saved.
    await expect(this.page.locator('.mat-badge-content')).toHaveText('1');
  }
}
