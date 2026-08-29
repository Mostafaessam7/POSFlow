import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { ConfirmDialogService } from './confirm-dialog.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { DialogBehaviorDirective } from '../dialog-behavior.directive';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe, DialogBehaviorDirective],
  template: `
    @if (service.pending(); as pending) {
      <div class="overlay">
        <div class="dialog" role="alertdialog" aria-modal="true" [attr.aria-label]="pending.request.title | t"
             appDialogBehavior (dismissed)="service.respondCancel()">
          <h2>{{ pending.request.title | t }}</h2>
          <p>{{ pending.request.message | t }}</p>

          @if (pending.request.withInput) {
            <input
              type="text"
              [placeholder]="pending.request.inputPlaceholder | t"
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
              {{ pending.request.confirmLabel | t }}
            </button>

            <button type="button" class="cancel" (click)="service.respondCancel()">
              {{ pending.request.cancelLabel | t }}
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
