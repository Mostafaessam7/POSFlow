import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  type: 'success' | 'error';
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private nextId = 1;

  readonly toasts = signal<Toast[]>([]);

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  dismiss(id: number): void {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }

  private show(message: string, type: Toast['type']): void {
    const toast: Toast = { id: this.nextId++, message, type };

    this.toasts.update(list => [...list, toast]);

    setTimeout(() => this.dismiss(toast.id), 4000);
  }
}
