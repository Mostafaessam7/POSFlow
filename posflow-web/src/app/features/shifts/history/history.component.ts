import {
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

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule],
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
      `هيتم إلغاء الفاتورة رقم ${order.orderNumber}. اكتب سبب الإلغاء:`,
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
        }
      });
  }

  goBack(): void {
    this.router.navigateByUrl('/open-shift');
  }
}
