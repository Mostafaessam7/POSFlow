import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ToastComponent } from './shared/toast/toast.component';
import { ConfirmDialogComponent } from './shared/confirm-dialog/confirm-dialog.component';
import { SettingsToggleComponent } from './shared/settings-toggle/settings-toggle.component';
import { ThemeService } from './core/theme/theme.service';
import { TranslationService } from './core/i18n/translation.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    ToastComponent,
    ConfirmDialogComponent,
    SettingsToggleComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  // Injected only to construct them eagerly at app startup, so
  // <html>'s data-theme/dir/lang are set before the first paint
  // instead of waiting for SettingsToggleComponent to mount.
  private readonly themeService = inject(ThemeService);
  private readonly translationService = inject(TranslationService);
}
