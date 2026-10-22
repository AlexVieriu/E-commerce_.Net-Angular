import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { RegisterPage } from '../pages/RegisterPage';
import { OrdersPage } from '../pages/OrdersPage';
import { HeaderNav } from '../pages/HeaderNav';

test.describe('registration (always exercises the real register path)', () => {
  // Starts logged out (empty storageState) with a fresh email, so it really registers.
  test.use({ storageState: { cookies: [], origins: [] } });

  test('a new visitor can register and then log in', async ({ page }) => {
    const email = `register.${Date.now()}@skinet-test.local`;
    const password = 'Pa$$w0rd12!@';

    const registerPage = new RegisterPage(page);
    await registerPage.goto();
    await registerPage.register({ firstName: 'New', lastName: 'Visitor', email, password });
    await expect(page).toHaveURL(/\/account\/login/);

    const loginPage = new LoginPage(page);
    await loginPage.login(email, password);
    await expect(page).toHaveURL(/\/shop/);

    const header = new HeaderNav(page);
    await header.expectLoggedInAs(email);
  });
});

test.describe('existing customer', () => {
  // Runs after checkout.spec.ts (`dependencies`), so this account already has an order.
  test('My Orders shows the order placed at checkout', async ({ page }) => {
    const orders = new OrdersPage(page);
    await orders.goto();
    await orders.expectAnyOrderVisible();
  });

  test('logout clears the session', async ({ page }) => {
    const header = new HeaderNav(page);
    await page.goto('/shop');
    await header.logout();

    await expect(page).toHaveURL('/');
    await header.expectLoggedOut();

    // Visiting a page that needs login checks the session for real, not just what the page shows.
    await page.goto('/checkout');
    await expect(page).toHaveURL(/\/account\/login/);
  });
});
