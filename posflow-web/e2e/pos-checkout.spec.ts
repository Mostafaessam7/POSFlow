import { test, expect } from '@playwright/test';

/**
 * The one flow that matters most in this whole app: log in, open a
 * shift, ring up a sale, see the receipt. Assumes a freshly-seeded
 * database (the "admin"/"Admin@123" account and demo catalog from
 * DatabaseSeeder.SeedDemoDataAsync) - see e2e/README.md.
 */
test('full POS flow: login, open shift, add product, checkout', async ({ page }) => {
  await page.goto('/login');
  await page.fill('#username', 'admin');
  await page.fill('#password', 'Admin@123');
  await page.click('button[type=submit]');

  await expect(page).toHaveURL(/\/open-shift$/);

  // Open a shift if one isn't already open from a previous run.
  const openingCashInput = page.locator('#openingCash');

  if (await openingCashInput.isVisible().catch(() => false)) {
    await openingCashInput.fill('100');
    await page.click('button[type=submit]');
    await expect(page.getByText('حالة الوردية')).toBeVisible();
  }

  await page.click('button:has-text("الانتقال إلى نقطة البيع")');
  await expect(page).toHaveURL(/\/pos$/);

  // Add the first available product tile to the cart.
  await page.locator('.product-tile').first().click();
  await expect(page.locator('.cart-line').first()).toBeVisible();

  await page.click('.checkout-button');

  await expect(page.getByText('تمت عملية البيع بنجاح')).toBeVisible({
    timeout: 10_000
  });
  await expect(page.getByText('رقم الفاتورة')).toBeVisible();
});
