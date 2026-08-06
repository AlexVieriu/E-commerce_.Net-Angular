import { test, expect } from '@playwright/test';
import { CART_A_EMAIL, CART_B_EMAIL, CART_PASSWORD, API_URL } from '../support/credentials';
import { LoginPage } from '../pages/LoginPage';
import { ShopPage } from '../pages/ShopPage';
import { CartPage } from '../pages/CartPage';
import { HeaderNav } from '../pages/HeaderNav';

// BUG-18 from the user's side: the cart used to follow the *browser*, not the person. The cart id
// lived in localStorage and survived logout, so the next person to log in on that machine inherited
// the previous one's items.
//
// This spec deliberately has no storageState. The other specs start already logged in; this one has
// to perform two real logins and a real logout in a single browser, because the logout is the bug.
test('a second user on the same browser never inherits the first user cart', async ({
  page,
  request
}) => {
  // Both accounts exist after the first run; the API 400s on a duplicate, which we ignore.
  for (const email of [CART_A_EMAIL, CART_B_EMAIL]) {
    await request.post(`${API_URL}account/register`, {
      data: { firstName: 'E2E', lastName: 'Cart', email, password: CART_PASSWORD },
      failOnStatusCode: false
    });
  }

  const loginPage = new LoginPage(page);
  const shopPage = new ShopPage(page);
  const cartPage = new CartPage(page);
  const header = new HeaderNav(page);

  // User A logs in and puts something in their cart.
  // addFirstProductToCart asserts the badge reaches "1", so the setup itself is verified.
  await loginPage.goto();
  await loginPage.login(CART_A_EMAIL, CART_PASSWORD);
  await page.waitForURL('**/shop');
  await shopPage.addFirstProductToCart();

  // User A leaves.
  await header.logout();
  await header.expectLoggedOut();

  // User B sits down at the same browser.
  await loginPage.goto();
  await loginPage.login(CART_B_EMAIL, CART_PASSWORD);
  await page.waitForURL('**/shop');
  await header.expectLoggedInAs(CART_B_EMAIL);

  // Before the fix, A's item was sitting right here.
  await cartPage.goto();
  await expect(page.getByText('Shopping cart is empty')).toBeVisible();
});
