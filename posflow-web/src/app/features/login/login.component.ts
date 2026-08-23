import {
  ChangeDetectorRef,
  Component,
  inject
} from '@angular/core';

import {
  CommonModule
} from '@angular/common';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import {
  Router
} from '@angular/router';

import {
  finalize
} from 'rxjs';

import {
  AuthService
} from '../../core/auth/auth.service';

import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly formBuilder =
    inject(FormBuilder);

  private readonly authService =
    inject(AuthService);

  private readonly router =
    inject(Router);

  private readonly cdr =
    inject(ChangeDetectorRef);

  isSubmitting = false;
  errorMessage = '';

  readonly form =
    this.formBuilder.nonNullable.group({
      username: [
        '',
        [
          Validators.required
        ]
      ],

      password: [
        '',
        [
          Validators.required,
          Validators.minLength(6)
        ]
      ]
    });

  submit(): void {
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;

    this.authService
      .login(this.form.getRawValue())
      .pipe(
        finalize(() => {
          this.isSubmitting = false;

          // See OpenShiftComponent.safeDetectChanges() for why this
          // call is here - same repro, same fix (Angular isn't
          // reliably re-rendering this view on its own after the
          // HTTP call resolves in this app's current build).
          try {
            this.cdr.detectChanges();
          } catch {
            // View already destroyed (navigated away) - nothing to
            // render.
          }
        })
      )
      .subscribe({
        next: () => {
          this.router.navigateByUrl(
            '/open-shift'
          );
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'حدث خطأ أثناء تسجيل الدخول';
        }
      });
  }

  goToForgotPassword(): void {
    this.router.navigateByUrl('/forgot-password');
  }
}