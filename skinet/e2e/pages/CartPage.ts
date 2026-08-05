import { Page } from '@playwright/test';

export class CartPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/cart');
  }

  async proceedToCheckout() {
    await this.page.getByRole('button', { name: 'Checkout' }).click();
  }
}
