import { test, expect } from '@playwright/test';

test.describe('Login', () => {
  test('rejects an invalid password with a visible error', async ({ page }) => {
    await page.goto('/login');

    await page.fill('#username', 'admin');
    await page.fill('#password', 'wrong-password');
    await page.click('button[type=submit]');

    await expect(page.locator('.api-error')).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);
  });

  test('a valid login redirects to the open-shift page', async ({ page }) => {
    await page.goto('/login');

    await page.fill('#username', 'admin');
    await page.fill('#password', 'Admin@123');
    await page.click('button[type=submit]');

    await expect(page).toHaveURL(/\/open-shift$/);
    await expect(page.getByText('إدارة الوردية')).toBeVisible();
  });
});
