import {
  Component,
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

import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
  private readonly formBuilder =
    inject(FormBuilder);

  private readonly authService =
    inject(AuthService);

  private readonly router =
    inject(Router);

  isSubmitting = false;
  isSubmitted = false;
  errorMessage = '';

  readonly form = this.formBuilder.nonNullable.group({
    username: ['', [Validators.required]]
  });

  submit(): void {
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;

    this.authService
      .forgotPassword(this.form.getRawValue())
      .pipe(
        finalize(() => {
          this.isSubmitting = false;
        })
      )
      .subscribe({
        next: () => {
          // Same message shown whether or not the username exists -
          // matches the backend's deliberately generic response.
          this.isSubmitted = true;
        },

        error: () => {
          this.errorMessage = 'حدث خطأ، حاول مرة أخرى';
        }
      });
  }

  goToLogin(): void {
    this.router.navigateByUrl('/login');
  }
}
