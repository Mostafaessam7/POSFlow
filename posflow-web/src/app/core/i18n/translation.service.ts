import { Injectable, signal } from '@angular/core';

import { EN_TRANSLATIONS } from './en-translations';

export type Lang = 'ar' | 'en';

const STORAGE_KEY = 'posflow_lang';

/**
 * Runtime i18n, Arabic-first: every template/component string IS its
 * own translation key (the Arabic source text), looked up in
 * EN_TRANSLATIONS for the English side and returned as-is for
 * Arabic. This avoids inventing a parallel key namespace across ~10
 * screens - a template just does `{{ 'نص عربي' | t }}`, and a
 * missing English entry falls back to the Arabic (never a blank
 * label). `dir`/`lang` on <html> follow the current language so RTL
 * layout switches automatically with no per-page code.
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly langSignal =
    signal<Lang>(this.readInitial());

  readonly lang = this.langSignal.asReadonly();

  constructor() {
    this.apply(this.langSignal());
  }

  t(key: string): string {
    if (this.langSignal() === 'ar') {
      return key;
    }

    return EN_TRANSLATIONS[key] ?? key;
  }

  toggle(): void {
    this.setLang(this.langSignal() === 'ar' ? 'en' : 'ar');
  }

  setLang(lang: Lang): void {
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Private browsing / storage disabled - just won't persist.
    }

    this.langSignal.set(lang);
    this.apply(lang);
  }

  private readInitial(): Lang {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);

      if (saved === 'ar' || saved === 'en') {
        return saved;
      }
    } catch {
      // Ignore - default below.
    }

    return 'ar';
  }

  private apply(lang: Lang): void {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }
}
