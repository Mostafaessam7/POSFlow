import { Pipe, PipeTransform, inject } from '@angular/core';

import { TranslationService } from './translation.service';

/**
 * `{{ 'نص عربي' | t }}`. Impure on purpose: a pure pipe only
 * re-evaluates when ITS OWN input changes, but the language toggle
 * lives in one shared widget (SettingsToggleComponent) - every other
 * screen's `| t` bindings need to re-run when that widget's click
 * flips TranslationService's language, not when their own key
 * happens to change.
 */
@Pipe({
  name: 't',
  standalone: true,
  pure: false
})
export class TranslatePipe implements PipeTransform {
  private readonly translationService =
    inject(TranslationService);

  transform(key: string): string {
    return this.translationService.t(key);
  }
}
