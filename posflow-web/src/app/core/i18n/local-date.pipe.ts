import { Pipe, PipeTransform, inject } from '@angular/core';

import { TranslationService } from './translation.service';

export type LocalDateStyle = 'medium' | 'shortTime';

/**
 * `{{ value | localDate }}` / `{{ value | localDate:'shortTime' }}`.
 * Angular's own `date` pipe reads LOCALE_ID once, at injection time -
 * fine for an app with one fixed locale, but this app's language
 * switches at runtime (TranslationService), and LOCALE_ID can't
 * follow that without a full app reload. Intl.DateTimeFormat has no
 * such restriction, so this pipe re-picks the locale on every render
 * (impure, same reasoning as TranslatePipe) instead of going through
 * Angular's locale system at all.
 */
@Pipe({
  name: 'localDate',
  standalone: true,
  pure: false
})
export class LocalDatePipe implements PipeTransform {
  private readonly translationService =
    inject(TranslationService);

  transform(
    value: Date | string | null | undefined,
    style: LocalDateStyle = 'medium'
  ): string {
    if (!value) {
      return '';
    }

    const date = value instanceof Date ? value : new Date(value);

    if (Number.isNaN(date.getTime())) {
      return '';
    }

    // -u-nu-latn: Western digits even in Arabic. "ar-EG" defaults to
    // Arabic-Indic numerals (١٢٣...), but the rest of the app (prices,
    // quantities, via the number pipe) always renders Western digits -
    // dates switching numeral systems on their own would look like a
    // second, inconsistent number style bolted onto the same screen.
    const locale =
      this.translationService.lang() === 'ar'
        ? 'ar-EG-u-nu-latn'
        : 'en-US';

    const options: Intl.DateTimeFormatOptions =
      style === 'shortTime'
        ? { hour: 'numeric', minute: '2-digit' }
        : {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: 'numeric',
            minute: '2-digit'
          };

    return new Intl.DateTimeFormat(locale, options).format(date);
  }
}
