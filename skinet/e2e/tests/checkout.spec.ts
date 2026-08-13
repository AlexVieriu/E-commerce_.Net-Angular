import { test, expect } from '@playwright/test';
import { ShopPage } from '../pages/ShopPage';
import { CartPage } from '../pages/CartPage';
import { CheckoutPage } from '../pages/CheckoutPage';

// This test walks through the whole checkout flow like a real customer would:
// add a product, go to checkout, fill in address + payment, pay, and land on the success page.
// It uses Stripe's official test card, so it's a real payment, just against Stripe's test mode.
test('customer can check out with a real Stripe payment', async ({ page }) => {
  const shop = new ShopPage(page);
  const cart = new CartPage(page);
  const checkout = new CheckoutPage(page);

  await shop.goto();
  await shop.addFirstProductToCart();

  await cart.goto();
  await cart.proceedToCheckout();

  await checkout.fillAddress({
    name: 'Ada Lovelace',
    line1: '1600 Amphitheatre Parkway',
    city: 'Mountain View',
    state: 'CA',
    postalCode: '94043'
  });
  await checkout.selectDelivery();
  await checkout.fillPayment({ number: '4242424242424242', expiry: '12/34', cvc: '123' });
  await checkout.pay();

  await expect(page).toHaveURL(/\/checkout\/success/);
});
