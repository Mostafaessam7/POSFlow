import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'posflow_theme';

/**
 * Light/dark is a single `data-theme` attribute on <html> - every
 * color in the app is already a CSS custom property (see
 * styles.scss), so this is the only place that needs to know the
 * mechanism. Persisted explicitly once the user picks one; before
 * that, falls back to the OS preference so a first-time visitor
 * doesn't get a jarring flash of the "wrong" theme.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly themeSignal =
    signal<ThemeMode>(this.readInitial());

  readonly theme = this.themeSignal.asReadonly();

  constructor() {
    this.apply(this.themeSignal());
  }

  toggle(): void {
    this.setTheme(this.themeSignal() === 'dark' ? 'light' : 'dark');
  }

  setTheme(theme: ThemeMode): void {
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Private browsing / storage disabled - theme just won't
      // persist across reloads, still works for this session.
    }

    this.themeSignal.set(theme);
    this.apply(theme);
  }

  private readInitial(): ThemeMode {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);

      if (saved === 'light' || saved === 'dark') {
        return saved;
      }
    } catch {
      // Ignore - fall through to the OS preference below.
    }

    return window.matchMedia?.('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light';
  }

  private apply(theme: ThemeMode): void {
    document.documentElement.setAttribute('data-theme', theme);
  }
}
