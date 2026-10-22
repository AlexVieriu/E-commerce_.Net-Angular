import { Page } from '@playwright/test';

export class RegisterPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/account/register');
  }

  async register(fields: { firstName: string; lastName: string; email: string; password: string }) {
    await this.page.getByLabel('First Name').fill(fields.firstName);
    await this.page.getByLabel('Last Name').fill(fields.lastName);
    await this.page.getByLabel('Email').fill(fields.email);
    await this.page.getByLabel('Password').fill(fields.password);
    // The header has a "Register" button too, so search inside the form only.
    await this.page.locator('form').getByRole('button', { name: 'Register' }).click();
  }
}
