import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ConfirmDialogService } from './confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (service.pending(); as pending) {
      <div class="overlay" dir="rtl" (keydown.escape)="service.respondCancel()">
        <div class="dialog" role="alertdialog" aria-modal="true" [attr.aria-label]="pending.request.title">
          <h2>{{ pending.request.title }}</h2>
          <p>{{ pending.request.message }}</p>

          @if (pending.request.withInput) {
            <input
              type="text"
              [placeholder]="pending.request.inputPlaceholder"
              [(ngModel)]="service.inputValue"
              (keydown.enter)="service.respondConfirm()"
              autofocus
            />
          }

          <div class="actions">
            <button
              type="button"
              class="confirm"
              [class.danger]="pending.request.danger"
              (click)="service.respondConfirm()">
              {{ pending.request.confirmLabel }}
            </button>

            <button type="button" class="cancel" (click)="service.respondCancel()">
              {{ pending.request.cancelLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrl: './confirm-dialog.component.scss'
})
export class ConfirmDialogComponent {
  readonly service = inject(ConfirmDialogService);
}
