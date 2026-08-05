import {
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

@Component({
  selector: 'app-open-shift',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule
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

  readonly isAdmin =
    this.authService.hasAnyRole(Roles.Admin);

  readonly isManager =
    this.authService.hasAnyRole(Roles.Manager);

  currentShift: ShiftResponse | null = null;

  isLoading = true;
  isSubmitting = false;

  errorMessage = '';
  successMessage = '';

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

loadCurrentShift(): void {
  this.isLoading = true;
  this.errorMessage = '';

  this.shiftService
    .getCurrent()
    .pipe(
      finalize(() => {
        this.isLoading = false;
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
        })
      )
      .subscribe({
        next: closedShift => {
          this.successMessage =
            `تم إغلاق الوردية. فرق النقدية: ` +
            `${closedShift.cashDifference ?? 0}`;

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