import { test, expect, type Page } from '@playwright/test';

/**
 * Keyboard behaviour of the confirm dialog.
 *
 * This has to run in a real browser, and the reason is worth recording. The
 * same checks were first written as unit tests, where they could not work:
 * jsdom performs no layout, so every element reports zero width, height and
 * client rects. The CDK's InteractivityChecker treats that as "not visible",
 * finds nothing tabbable, and never moves focus -- the test reports a failure
 * that exists only in jsdom. Tab does not move focus there either. Anything
 * asserting where focus actually goes belongs here.
 *
 * The dialog's markup was already correct before this was written -- role,
 * aria-modal, aria-label and a (keydown.escape) binding all present, and axe
 * passed it. Measured behaviour was still:
 *
 *   focusInsideDialog=false (BUTTON) | escapeFromOutside=STILL_OPEN
 *
 * Nothing moved focus into the dialog, and Escape was bound to the .overlay
 * div, which never received the keydown: focus sat on the trigger outside the
 * overlay, so the event bubbled up a different branch of the tree. Correct
 * markup, no working behaviour. Only a real browser catches that.
 */

const TAB_ATTEMPTS = 20;
const DIALOG = '.dialog';

async function logIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.fill('#username', 'admin');
  await page.fill('#password', 'Admin@123');
  await page.click('button[type=submit]');
  await expect(page).toHaveURL(/\/open-shift$/);
}

/** Describes the focused element, or null when focus is inside the dialog. */
async function focusOutsideDialog(page: Page): Promise<string | null> {
  return page.evaluate((sel) => {
    const el = document.activeElement as HTMLElement | null;
    if (!el || el === document.body) return 'BODY';
    return el.closest(sel) ? null : `${el.tagName}.${el.className || '(no class)'}`.slice(0, 60);
  }, DIALOG);
}

test.describe('Confirm dialog keyboard behaviour', () => {
  test('traps focus, closes on Escape, and restores focus to the trigger', async ({ page }) => {
    await logIn(page);
    await page.goto('/products');

    const trigger = page.locator('.link-button.danger').first();
    await expect(trigger).toBeVisible();
    await trigger.click();

    const dialog = page.locator(DIALOG);
    await expect(dialog).toBeVisible();

    // The markup half, which was already right - asserted so it stays right.
    await expect(dialog).toHaveAttribute('role', 'alertdialog');
    await expect(dialog).toHaveAttribute('aria-modal', 'true');
    expect(await dialog.getAttribute('aria-label')).toBeTruthy();

    // The behaviour half, which was not.
    expect(await focusOutsideDialog(page), 'focus stayed outside the dialog on open').toBeNull();

    const escaped: string[] = [];
    for (let i = 0; i < TAB_ATTEMPTS; i++) {
      await page.keyboard.press('Tab');
      const outside = await focusOutsideDialog(page);
      if (outside) escaped.push(`tab ${i + 1}: ${outside}`);
    }
    expect(
      escaped,
      `focus escaped the dialog onto the page behind it:\n  ${escaped.slice(0, 5).join('\n  ')}`
    ).toEqual([]);

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();

    // Without restoration the user is dropped back at the top of the document
    // and has to tab all the way to where they were.
    await expect(trigger).toBeFocused();
  });
});
