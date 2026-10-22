import { test, expect } from '@playwright/test';
import { AdminOrdersPage } from '../pages/AdminOrdersPage';

// No exact order total: checkout.spec.ts adds orders on every run.
// The check is paging itself — page 1 = rows 1-10, page 2 = rows 11-20 (BUG-12).
test('admin orders table paginates correctly (would have caught BUG-12)', async ({ page }) => {
  const adminOrders = new AdminOrdersPage(page);
  await adminOrders.goto();

  await expect(adminOrders.rows).toHaveCount(10);
  await expect(page.locator('.mat-mdc-paginator-range-label')).toContainText('1 – 10 of');

  await adminOrders.goToNextPage();

  await expect(adminOrders.rows).toHaveCount(10);
  await expect(page.locator('.mat-mdc-paginator-range-label')).toContainText('11 – 20 of');
});
