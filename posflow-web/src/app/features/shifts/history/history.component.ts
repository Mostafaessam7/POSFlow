import {
  ChangeDetectorRef,
  Component,
  OnInit,
  inject
} from '@angular/core';

import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/auth/roles';
import { ToastService } from '../../../shared/toast/toast.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { ShiftService } from '../shift.service';
import { ShiftResponse } from '../shift.models';
import { OrderService } from '../../pos/order.service';
import { OrderResponse } from '../../pos/order.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss'
})
export class HistoryComponent
  implements OnInit {

  private readonly shiftService =
    inject(ShiftService);

  private readonly orderService =
    inject(OrderService);

  private readonly authService =
    inject(AuthService);

  private readonly toastService =
    inject(ToastService);

  private readonly confirmDialog =
    inject(ConfirmDialogService);

  private readonly router =
    inject(Router);

  private readonly cdr =
    inject(ChangeDetectorRef);

  private readonly translationService =
    inject(TranslationService);

  readonly canViewBranch =
    this.authService.hasAnyRole(Roles.Admin, Roles.Manager);

  readonly canVoidAnyOrder =
    this.authService.hasAnyRole(Roles.Admin, Roles.Manager);

  showBranchWide = false;

  shifts: ShiftResponse[] = [];
  page = 1;
  totalPages = 1;

  isLoading = true;
  errorMessage = '';

  expandedShiftId: string | null = null;
  ordersByShift: Record<string, OrderResponse[]> = {};
  isLoadingOrders = false;

  ngOnInit(): void {
    this.loadHistory();
  }

  toggleScope(): void {
    this.showBranchWide = !this.showBranchWide;
    this.page = 1;
    this.expandedShiftId = null;
    this.ordersByShift = {};
    this.loadHistory();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) {
      return;
    }

    this.page = page;
    this.loadHistory();
  }

  loadHistory(): void {
    this.isLoading = true;
    this.errorMessage = '';

    const request$ = this.showBranchWide
      ? this.shiftService.getBranchHistory(this.page)
      : this.shiftService.getHistory(this.page);

    request$
      .pipe(
        finalize(() => {
          this.isLoading = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: result => {
          this.shifts = result.items;
          this.totalPages = result.totalPages;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تحميل سجل الورديات';
        }
      });
  }

  toggleShift(shift: ShiftResponse): void {
    if (this.expandedShiftId === shift.id) {
      this.expandedShiftId = null;
      return;
    }

    this.expandedShiftId = shift.id;
    this.loadShiftOrders(shift.id);
  }

  loadShiftOrders(shiftId: string): void {
    this.isLoadingOrders = true;

    this.orderService
      .getByShiftId(shiftId)
      .pipe(
        finalize(() => {
          this.isLoadingOrders = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: result => {
          this.ordersByShift[shiftId] = result.items;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تحميل فواتير الوردية';
        }
      });
  }

  canVoid(shift: ShiftResponse): boolean {
    return this.canVoidAnyOrder || shift.status === 'Open';
  }

  async voidOrder(shift: ShiftResponse, order: OrderResponse): Promise<void> {
    if (order.status !== 'Completed') {
      return;
    }

    const reason = await this.confirmDialog.prompt(
      this.translationService.t('هيتم إلغاء الفاتورة رقم') +
        ` ${order.orderNumber}. ` +
        this.translationService.t('اكتب سبب الإلغاء:'),
      {
        title: 'إلغاء فاتورة',
        confirmLabel: 'إلغاء الفاتورة',
        inputPlaceholder: 'سبب الإلغاء...'
      }
    );

    if (!reason) {
      return;
    }

    this.orderService
      .voidOrder(order.id, { reason })
      .subscribe({
        next: () => {
          this.toastService.success('تم إلغاء الفاتورة');
          this.loadShiftOrders(shift.id);
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر إلغاء الفاتورة';
          this.safeDetectChanges();
        }
      });
  }

  goBack(): void {
    this.router.navigateByUrl('/open-shift');
  }

  /**
   * Angular isn't reliably re-rendering this view on its own after an
   * HTTP call resolves, in this app's current build - see
   * OpenShiftComponent.safeDetectChanges() for the full repro notes.
   * Wrapped defensively against an already-destroyed view.
   */
  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // View already destroyed - nothing to render.
    }
  }
}
