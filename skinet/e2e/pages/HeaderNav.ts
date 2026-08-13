import { Page, expect } from '@playwright/test';

export class HeaderNav {
  constructor(private page: Page) {}

  // When logged in, the header shows a button with the user's email as its text.
  // We find it by looking for "@" instead of a full email, so it works for any account.
  private get userMenuButton() {
    return this.page.getByRole('button', { name: /@/ });
  }

  async expectLoggedInAs(email: string) {
    await expect(this.userMenuButton).toContainText(email);
  }

  async expectLoggedOut() {
    await expect(this.page.getByRole('button', { name: 'Login' })).toBeVisible();
  }

  async logout() {
    await this.userMenuButton.click();
    await this.page.getByRole('menuitem', { name: 'Logout' }).click();
  }

  // The cart icon badge. Absent when the cart is empty.
  async expectCartCount(count: string) {
    await expect(this.page.locator('.mat-badge-content')).toHaveText(count);
  }
}
