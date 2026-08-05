import { test as setup } from '@playwright/test';
import path from 'path';
import { CUSTOMER_EMAIL, CUSTOMER_PASSWORD, API_URL } from '../../support/credentials';
import { LoginPage } from '../../pages/LoginPage';

const authFile = path.join(__dirname, '../../playwright/.auth/customer.json');

setup('authenticate as customer', async ({ page, request }) => {
  // Make sure this account exists before we try to log in.
  // First time this runs, it creates the account. Every time after, the account already
  // exists, so this call fails -- and we just ignore that, since that's expected.
  await request.post(`${API_URL}account/register`, {
    data: { firstName: 'E2E', lastName: 'Customer', email: CUSTOMER_EMAIL, password: CUSTOMER_PASSWORD },
    failOnStatusCode: false
  });

  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.login(CUSTOMER_EMAIL, CUSTOMER_PASSWORD);
  await page.waitForURL('**/shop');

  // Save the logged-in session (cookies) to a file. Other tests load this file instead
  // of logging in again themselves -- see playwright.config.ts's `storageState`.
  await page.context().storageState({ path: authFile });
});
