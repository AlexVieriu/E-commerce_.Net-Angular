import { test as setup } from '@playwright/test';
import path from 'path';
import { ADMIN_EMAIL, ADMIN_PASSWORD } from '../../support/credentials';
import { LoginPage } from '../../pages/LoginPage';

const authFile = path.join(__dirname, '../../playwright/.auth/admin.json');

setup('authenticate as admin', async ({ page }) => {
  const loginPage = new LoginPage(page);
  await loginPage.goto();
  await loginPage.login(ADMIN_EMAIL, ADMIN_PASSWORD);
  await page.waitForURL('**/shop');

  await page.context().storageState({ path: authFile });
});
