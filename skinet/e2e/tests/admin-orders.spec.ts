import { test, expect } from '@playwright/test';
import { AdminOrdersPage } from '../pages/AdminOrdersPage';

// We don't check the exact total number of orders -- it grows a little every time this suite
// runs (checkout.spec.ts adds one more order each time). What we do check is that paging
// actually works: page 1 shows rows 1-10, page 2 shows rows 11-20. That's the part that
// broke before (BUG-12) and the part worth testing.
test('admin orders table paginates correctly (would have caught BUG-12)', async ({ page }) => {
  const adminOrders = new AdminOrdersPage(page);
  await adminOrders.goto();

  await expect(adminOrders.rows).toHaveCount(10);
  await expect(page.locator('.mat-mdc-paginator-range-label')).toContainText('1 – 10 of');

  await adminOrders.goToNextPage();

  await expect(adminOrders.rows).toHaveCount(10);
  await expect(page.locator('.mat-mdc-paginator-range-label')).toContainText('11 – 20 of');
});
