import { Directive, EventEmitter, Output, inject } from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';

/**
 * Gives a dialog the keyboard behaviour its ARIA markup already promises:
 * focus moves in on open, cannot Tab out, and returns to the trigger on close.
 *
 * Why this exists. The confirm dialog already declared role="alertdialog",
 * aria-modal="true", an aria-label, and a (keydown.escape) handler -- it reads
 * as correct, and axe passes it. A probe against the real component measured:
 *
 *   role=alertdialog | ariaModal=true | focusInsideDialog=false (BUTTON)
 *                                     | escapeFromOutside=STILL_OPEN
 *
 * Nothing ever moved focus into the dialog, so focus stayed on the button that
 * opened it. Escape was bound to the .overlay div, and a div is not focusable:
 * the keydown fires on the focused trigger and bubbles up through *its*
 * ancestors, which do not include the overlay. The handler was unreachable.
 * Announcing a modal while leaving the page behind it fully tabbable is the
 * more serious half -- a screen-reader user is told the rest is inert when it
 * is not.
 *
 * This directive deliberately sets no ARIA. The template's role/aria-modal/
 * aria-label are already right, and alertdialog is the better role for a
 * confirm than plain dialog; overwriting them here would be a regression.
 * Only the missing behaviour is added.
 *
 * Usage: <div class="dialog" role="alertdialog" appDialogBehavior (dismissed)="cancel()">
 */
@Directive({
  selector: '[appDialogBehavior]',
  standalone: true,
  hostDirectives: [CdkTrapFocus],
  host: {
    // Lets the container itself hold focus when it contains nothing focusable,
    // so the CDK always has somewhere to put it rather than leaving it outside.
    tabindex: '-1',
    '(keydown.escape)': 'onEscape($event)'
  }
})
export class DialogBehaviorDirective {
  @Output() dismissed = new EventEmitter<void>();

  constructor() {
    // Moves focus inside on open and restores it to the previously focused
    // element on close. Host directives are constructed before this one, so
    // the flag is set before the CDK's ngAfterContentInit reads it.
    inject(CdkTrapFocus).autoCapture = true;
  }

  onEscape(event: Event): void {
    event.stopPropagation();
    this.dismissed.emit();
  }
}
