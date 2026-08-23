import {
  ChangeDetectorRef,
  Component,
  OnInit,
  inject
} from '@angular/core';

import { CommonModule } from '@angular/common';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { ShiftResponse } from '../shift.models';
import { ShiftService } from '../shift.service';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/auth/roles';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { LocalDatePipe } from '../../../core/i18n/local-date.pipe';

@Component({
  selector: 'app-open-shift',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    LocalDatePipe
  ],
  templateUrl: './open-shift.component.html',
  styleUrl: './open-shift.component.scss'
})
export class OpenShiftComponent
  implements OnInit {

  private readonly formBuilder =
    inject(FormBuilder);

  private readonly shiftService =
    inject(ShiftService);

  private readonly router =
    inject(Router);

  private readonly authService =
    inject(AuthService);

  // See loadCurrentShift()/openShift()/closeShift() below for why this
  // is here.
  private readonly cdr =
    inject(ChangeDetectorRef);

  readonly isAdmin =
    this.authService.hasAnyRole(Roles.Admin);

  readonly isManager =
    this.authService.hasAnyRole(Roles.Manager);

  currentShift: ShiftResponse | null = null;

  isLoading = true;
  isSubmitting = false;

  errorMessage = '';
  successMessage = '';
  closedCashDifference: number | null = null;

  readonly openForm =
    this.formBuilder.nonNullable.group({
      openingCash: [
        0,
        [
          Validators.required,
          Validators.min(0)
        ]
      ]
    });

  readonly closeForm =
    this.formBuilder.nonNullable.group({
      closingCash: [
        0,
        [
          Validators.required,
          Validators.min(0)
        ]
      ]
    });

  ngOnInit(): void {
    this.loadCurrentShift();
  }

  /**
   * Angular isn't reliably re-rendering this view on its own after an
   * HTTP call resolves, in this app's current build (confirmed live:
   * the component's fields update correctly, but the DOM stays on
   * stale content - even a stray click or an explicit
   * ApplicationRef.tick() doesn't help - until something forces a
   * check directly on this view). detectChanges() bypasses whatever
   * is skipping the automatic pass. Wrapped defensively: a response
   * that resolves after the user has already navigated away (e.g.
   * openShift()'s success path redirects to /pos before this runs)
   * would otherwise throw on an already-destroyed view.
   */
  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // View already destroyed (navigated away) - nothing to render.
    }
  }

loadCurrentShift(): void {
  this.isLoading = true;
  this.errorMessage = '';

  this.shiftService
    .getCurrent()
    .pipe(
      finalize(() => {
        this.isLoading = false;
        this.safeDetectChanges();
      })
    )
    .subscribe({
      next: response => {
        this.currentShift = response.shift;
      },

      error: error => {
        console.error(
          'Get current shift error:',
          error
        );

        this.errorMessage =
          error?.error?.message ??
          'تعذر تحميل بيانات الوردية';
      }
    });
}
  openShift(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (this.openForm.invalid) {
      this.openForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;

    this.shiftService
      .open(this.openForm.getRawValue())
      .pipe(
        finalize(() => {
          this.isSubmitting = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: shift => {
          this.currentShift = shift;

          this.router.navigateByUrl('/pos');
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر فتح الوردية';
        }
      });
  }

  closeShift(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.currentShift) {
      return;
    }

    if (this.closeForm.invalid) {
      this.closeForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;

    this.shiftService
      .close(
        this.currentShift.id,
        this.closeForm.getRawValue()
      )
      .pipe(
        finalize(() => {
          this.isSubmitting = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: closedShift => {
          // Kept as a translatable label + a separate numeric value
          // (not one interpolated string) so the "| t" pipe in the
          // template has a stable, exact key to look up regardless of
          // what the cash difference number is.
          this.successMessage = 'تم إغلاق الوردية. فرق النقدية:';
          this.closedCashDifference = closedShift.cashDifference ?? 0;

          this.currentShift = null;

          this.openForm.reset({
            openingCash: 0
          });

          this.closeForm.reset({
            closingCash: 0
          });
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر إغلاق الوردية';
        }
      });
  }

  continueToPos(): void {
    this.router.navigateByUrl('/pos');
  }

  goToHistory(): void {
    this.router.navigateByUrl('/history');
  }

  goToDashboard(): void {
    this.router.navigateByUrl('/dashboard');
  }

  goToUsers(): void {
    this.router.navigateByUrl('/admin/users');
  }

  goToBranches(): void {
    this.router.navigateByUrl('/admin/branches');
  }

  logout(): void {
    this.authService.logout();
    this.router.navigateByUrl('/login');
  }
}