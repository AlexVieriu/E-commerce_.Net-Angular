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

    // "Add to cart" sends a request in the background. If we navigate away too fast,
    // that request can get cancelled and the item never actually gets added.
    // So we wait here until the cart icon's badge shows "1" -- proof it really worked.
    await expect(this.page.locator('.mat-badge-content')).toHaveText('1');
  }
}
