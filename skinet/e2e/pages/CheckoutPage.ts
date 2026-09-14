import { Page, FrameLocator, expect } from '@playwright/test';

// A "page object": one class per app page, holding its buttons/fields as methods.
// Tests call these methods instead of repeating selectors -- easier to read, easier to fix.
export class CheckoutPage {
  constructor(private page: Page) {}

  // Stripe's card/address forms live inside iframes (separate embedded pages).
  // frameLocator() is how Playwright reaches inside an iframe to click/fill things.
  private addressFrame(): FrameLocator {
    return this.page.frameLocator('iframe[title="Secure address input frame"]');
  }

  private paymentFrame(): FrameLocator {
    return this.page.frameLocator('iframe[title="Secure payment input frame"]');
  }

  async goto() {
    await this.page.goto('/checkout');
  }

  // Sometimes the first click on "Next" doesn't work -- the form thinks it's still validating.
  // So we click, wait a bit, check if the step moved on, and click again if it didn't (up to 5 times).
  private async clickNextUntilStepActive(stepName: string) {
    const nextButton = this.page.getByRole('button', { name: 'Next' });
    const stepHeader = this.page.locator('.mat-step-header', { hasText: stepName });

    for (let attempt = 0; attempt < 5; attempt++) {
      if ((await stepHeader.getAttribute('aria-selected')) === 'true')
        return;
      await nextButton.click();
      await this.page.waitForTimeout(1000);
    }

    await expect(stepHeader).toHaveAttribute('aria-selected', 'true');
  }

  async fillAddress(address: { name: string; line1: string; city: string; state: string; postalCode: string }) {
    const frame = this.addressFrame();
    // We pick the country ourselves (US) instead of trusting the auto-detected one --
    // it's not always the same, and the State field only shows up for some countries.
    await frame.locator('select[name="country"]').selectOption('US');
    await frame.locator('#shippingAddress-nameInput').fill(address.name);
    await frame.locator('#shippingAddress-addressLine1Input').fill(address.line1);
    await this.page.keyboard.press('Escape'); // closes the address-suggestions popup, if one opened
    await frame.locator('#shippingAddress-localityInput').fill(address.city);
    await frame.locator('select[name="administrativeArea"]').selectOption(address.state);
    await frame.locator('#shippingAddress-postalCodeInput').fill(address.postalCode);

    await this.clickNextUntilStepActive('Shipping');
  }

  async selectDelivery() {
    await this.page.locator('mat-radio-button').first().click();
    await this.clickNextUntilStepActive('Payment');
  }

  async fillPayment(card: { number: string; expiry: string; cvc: string }) {
    const frame = this.paymentFrame();
    await frame.locator('#payment-numberInput').fill(card.number);
    await frame.locator('#payment-expiryInput').fill(card.expiry);
    await frame.locator('#payment-cvcInput').fill(card.cvc);

    await this.clickNextUntilStepActive('Confirmation');
  }

  async pay() {
    await this.clickPay();
    await this.page.waitForURL('**/checkout/success');
  }

  // Pays with a card Stripe declines. Returns the message the app showed.
  async payExpectingDecline(): Promise<string> {
    await this.clickPay();

    // .snack-error is the panelClass SnackbarService puts on the error snackbar.
    // Material nests two elements with the label class, so take the outer one.
    const snackbar = this.page.locator('.snack-error');
    const message = snackbar.locator('.mat-mdc-snack-bar-label').first();
    await message.waitFor({ timeout: 30_000 });
    const text = (await message.textContent())?.trim() ?? '';

    // Close it, so it can't sit over the Pay button on the retry.
    await snackbar.getByRole('button', { name: 'Close' }).click();
    await snackbar.waitFor({ state: 'detached' });

    return text;
  }

  private async clickPay() {
    await this.page.getByText('Billing and delivery information').waitFor();
    await this.page.getByRole('button', { name: /^Pay/ }).click();
  }
}
