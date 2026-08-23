import { Component, inject } from '@angular/core';

import { ThemeService } from '../../core/theme/theme.service';
import { TranslationService } from '../../core/i18n/translation.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/**
 * One small floating control, mounted once in the app shell (see
 * app.html) so it's present on every screen without every page's own
 * header needing to know about it.
 */
@Component({
  selector: 'app-settings-toggle',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './settings-toggle.component.html',
  styleUrl: './settings-toggle.component.scss'
})
export class SettingsToggleComponent {
  readonly themeService = inject(ThemeService);
  readonly translationService = inject(TranslationService);

  toggleTheme(): void {
    this.themeService.toggle();
  }

  toggleLang(): void {
    this.translationService.toggle();
  }
}
