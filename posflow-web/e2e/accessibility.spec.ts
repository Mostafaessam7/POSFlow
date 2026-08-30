import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

/**
 * Automated accessibility checks on the real running app.
 *
 * These complement `dialog-a11y.spec.ts` rather than overlapping it, and the split matters:
 * axe reads the DOM, so it catches what is *in* the markup — an input with no associated label, a
 * broken heading order, a contrast ratio that fails once the theme is applied. It cannot catch
 * behaviour. The confirm dialog in this app passed axe while focus never entered it and Escape did
 * nothing, which is exactly why that file exists separately.
 *
 * Scoped to wcag2a/wcag2aa deliberately. Running every axe rule pulls in best-practice and
 * experimental checks whose noise gets the whole gate ignored within a week.
 *
 * This is an RTL Arabic interface, which is worth stating because it is the case most likely to
 * produce a genuine finding here — direction and language attributes are easy to get wrong and
 * invisible to anyone testing in English.
 */

const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function logIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.fill('#username', 'admin');
  await page.fill('#password', 'Admin@123');
  await page.click('button[type=submit]');
  await expect(page).toHaveURL(/\/open-shift$/);
}

/** Renders violations usefully: a bare count tells you nothing about what to fix or where. */
function describe(violations: Awaited<ReturnType<AxeBuilder['analyze']>>['violations']): string {
  return violations
    .map((v) => {
      const where = v.nodes.slice(0, 3).map((n) => n.target.join(' ')).join('\n      ');
      return `  [${v.impact}] ${v.id}: ${v.help}\n    ${v.helpUrl}\n    elements:\n      ${where}`;
    })
    .join('\n\n');
}

async function expectNoViolations(page: Page, label: string): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(WCAG).analyze();

  expect(
    results.violations,
    `${label} has ${results.violations.length} accessibility violation(s):\n\n${describe(results.violations)}`
  ).toEqual([]);
}

test.describe('Accessibility', () => {
  test('the login page has no WCAG A/AA violations', async ({ page }) => {
    await page.goto('/login');
    await expect(page.locator('#username')).toBeVisible();

    await expectNoViolations(page, 'Login page');
  });

  test('the shift page has no WCAG A/AA violations', async ({ page }) => {
    await logIn(page);

    await expectNoViolations(page, 'Open-shift page');
  });

  test('the register screen has no WCAG A/AA violations', async ({ page }) => {
    await logIn(page);
    await page.goto('/pos');

    // The busiest screen in the product and the one staff use all day, so it is the one where a
    // missing label or an unreachable control costs the most.
    await expectNoViolations(page, 'POS register screen');
  });

  test('dark mode has no WCAG A/AA violations', async ({ page }) => {
    // Contrast is computed from what is actually painted, so the dark theme has to be checked
    // separately — a palette can pass in one mode and fail in the other.
    //
    // Set before navigating, then loaded fresh, rather than toggled on a rendered page: toggling
    // updates the tokens but leaves already-painted backgrounds, which produces contrast
    // "failures" that do not exist for a real user who loaded the page in that mode.
    await page.addInitScript(() => localStorage.setItem('posflow_theme', 'dark'));
    await page.goto('/login');
    await expect(page.locator('#username')).toBeVisible();

    await expectNoViolations(page, 'Login page (dark mode)');
  });
});
