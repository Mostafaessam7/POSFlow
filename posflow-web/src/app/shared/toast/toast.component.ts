import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-stack" dir="rtl" role="status" aria-live="polite">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast" [class.error]="toast.type === 'error'">
          <span>{{ toast.message }}</span>
          <button
            type="button"
            aria-label="إغلاق الإشعار"
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
