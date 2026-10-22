import { test as setup } from '@playwright/test';
import path from 'path';
import { CUSTOMER_EMAIL, CUSTOMER_PASSWORD, API_URL } from '../../support/credentials';
import { LoginPage } from '../../pages/LoginPage';

const authFile = path.join(__dirname, '../../playwright/.auth/customer.json');

setup('authenticate as customer', async ({ page, request }) => {
  // Register first; after the first run it fails as a duplicate, which is expected and ignored.
  await request.post(`${API_URL}account/register`, {
    data: { firstName: 'E2E', lastName: 'Customer', email: CUSTOMER_EMAIL, password: CUSTOMER_PASSWORD },
    failOnStatusCode: false
  });

  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.login(CUSTOMER_EMAIL, CUSTOMER_PASSWORD);
  await page.waitForURL('**/shop');

  // Save the session (cookies); other projects load it via `storageState`.
  await page.context().storageState({ path: authFile });
});
