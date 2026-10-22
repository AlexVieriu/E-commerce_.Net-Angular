import { test, expect } from '@playwright/test';
import { ShopPage } from '../pages/ShopPage';
import { CartPage } from '../pages/CartPage';
import { CheckoutPage } from '../pages/CheckoutPage';

// Both tests are the same customer, so the same cart. Serial keeps them from running at once.
test.describe.configure({ mode: 'serial' });

const ADDRESS = {
  name: 'Ada Lovelace',
  line1: '1600 Amphitheatre Parkway',
  city: 'Mountain View',
  state: 'CA',
  postalCode: '94043'
};

// The full checkout as a customer: add, address, pay, success page.
// A real payment with Stripe's test card, in test mode.
test('customer can check out with a real Stripe payment', async ({ page }) => {
  const shop = new ShopPage(page);
  const cart = new CartPage(page);
  const checkout = new CheckoutPage(page);

  await shop.goto();
  await shop.addFirstProductToCart();

  await cart.goto();
  await cart.proceedToCheckout();

  await checkout.fillAddress(ADDRESS);
  await checkout.selectDelivery();
  await checkout.fillPayment({ number: '4242424242424242', expiry: '12/34', cvc: '123' });
  await checkout.pay();

  await expect(page).toHaveURL(/\/checkout\/success/);
});

// BUG-2: creating the order deleted the cart before the charge, so a decline left nothing to retry —
// the customer saw "Cart unavailable" instead of Stripe's message.
test('a declined card can be retried in the same session', async ({ page }) => {
  const shop = new ShopPage(page);
  const cart = new CartPage(page);
  const checkout = new CheckoutPage(page);

  await shop.goto();
  await shop.addFirstProductToCart();

  await cart.goto();
  await cart.proceedToCheckout();

  await checkout.fillAddress(ADDRESS);
  await checkout.selectDelivery();
  await checkout.fillPayment({ number: '4000000000000002', expiry: '12/34', cvc: '123' });

  const message = await checkout.payExpectingDecline();
  expect(message).toMatch(/declined/i);

  // Still on Payment, cart intact: another card pays, with no reload.
  await checkout.fillPayment({ number: '4242424242424242', expiry: '12/34', cvc: '123' });
  await checkout.pay();

  await expect(page).toHaveURL(/\/checkout\/success/);
});
