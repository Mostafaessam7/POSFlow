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

import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss'
})
export class ResetPasswordComponent
  implements OnInit {

  private readonly formBuilder =
    inject(FormBuilder);

  private readonly authService =
    inject(AuthService);

  private readonly route =
    inject(ActivatedRoute);

  private readonly router =
    inject(Router);

  private token = '';

  hasToken = true;
  isSubmitting = false;
  isSubmitted = false;
  errorMessage = '';

  readonly form = this.formBuilder.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', [Validators.required]]
  });

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    this.hasToken = !!this.token;
  }

  submit(): void {
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    if (value.newPassword !== value.confirmPassword) {
      this.errorMessage = 'كلمة المرور وتأكيدها غير متطابقين';
      return;
    }

    this.isSubmitting = true;

    this.authService
      .resetPasswordWithToken({
        token: this.token,
        newPassword: value.newPassword
      })
      .pipe(
        finalize(() => {
          this.isSubmitting = false;
        })
      )
      .subscribe({
        next: () => {
          this.isSubmitted = true;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'الرابط غير صالح أو منتهي الصلاحية';
        }
      });
  }

  goToLogin(): void {
    this.router.navigateByUrl('/login');
  }

  goToForgotPassword(): void {
    this.router.navigateByUrl('/forgot-password');
  }
}
