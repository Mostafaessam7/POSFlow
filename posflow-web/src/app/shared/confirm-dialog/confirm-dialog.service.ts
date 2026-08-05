import { Injectable, signal } from '@angular/core';

export interface ConfirmRequest {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  danger: boolean;
  /** If set, renders a text input and resolves with its value (or null on cancel) instead of a boolean. */
  withInput: boolean;
  inputPlaceholder: string;
}

interface PendingRequest {
  request: ConfirmRequest;
  resolve: (value: string | boolean | null) => void;
}

@Injectable({
  providedIn: 'root'
})
export class ConfirmDialogService {
  readonly pending = signal<PendingRequest | null>(null);
  inputValue = '';

  /** Yes/no confirmation. Resolves true if confirmed, false if cancelled. */
  confirm(
    message: string,
    options?: Partial<Pick<ConfirmRequest, 'title' | 'confirmLabel' | 'cancelLabel' | 'danger'>>
  ): Promise<boolean> {
    return new Promise(resolve => {
      this.inputValue = '';

      this.pending.set({
        request: {
          title: options?.title ?? 'تأكيد',
          message,
          confirmLabel: options?.confirmLabel ?? 'تأكيد',
          cancelLabel: options?.cancelLabel ?? 'إلغاء',
          danger: options?.danger ?? false,
          withInput: false,
          inputPlaceholder: ''
        },
        resolve: value => resolve(value === true)
      });
    });
  }

  /** Confirmation with a required text input. Resolves the trimmed text, or null if cancelled/empty. */
  prompt(
    message: string,
    options?: Partial<Pick<ConfirmRequest, 'title' | 'confirmLabel' | 'cancelLabel' | 'inputPlaceholder'>>
  ): Promise<string | null> {
    return new Promise(resolve => {
      this.inputValue = '';

      this.pending.set({
        request: {
          title: options?.title ?? 'تأكيد',
          message,
          confirmLabel: options?.confirmLabel ?? 'تأكيد',
          cancelLabel: options?.cancelLabel ?? 'إلغاء',
          danger: false,
          withInput: true,
          inputPlaceholder: options?.inputPlaceholder ?? ''
        },
        resolve: value => resolve(typeof value === 'string' ? value : null)
      });
    });
  }

  respondConfirm(): void {
    const current = this.pending();

    if (!current) {
      return;
    }

    if (current.request.withInput) {
      const trimmed = this.inputValue.trim();
      current.resolve(trimmed ? trimmed : null);
    } else {
      current.resolve(true);
    }

    this.pending.set(null);
  }

  respondCancel(): void {
    const current = this.pending();

    if (!current) {
      return;
    }

    current.resolve(current.request.withInput ? null : false);
    this.pending.set(null);
  }
}
