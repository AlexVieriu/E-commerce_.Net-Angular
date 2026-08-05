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
    // The header also has a "Register" button, so we look only inside the form.
    await this.page.locator('form').getByRole('button', { name: 'Register' }).click();
  }
}
