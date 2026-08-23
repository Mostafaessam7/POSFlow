import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ToastService } from './toast.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    <div class="toast-stack" role="status" aria-live="polite">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast" [class.error]="toast.type === 'error'">
          <span>{{ toast.message | t }}</span>
          <button
            type="button"
            [attr.aria-label]="'إغلاق الإشعار' | t"
            (click)="toastService.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `,
  styleUrl: './toast.component.scss'
})
export class ToastComponent {
  readonly toastService = inject(ToastService);
}
