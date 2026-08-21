import {
  ChangeDetectorRef,
  Component,
  OnInit,
  inject
} from '@angular/core';

import { CommonModule } from '@angular/common';

import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/auth/roles';
import { ToastService } from '../../../shared/toast/toast.service';
import { BranchResponse } from '../branches/branch.models';
import { BranchService } from '../branches/branch.service';
import { UserResponse } from './user.models';
import { UserService } from './user.service';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './users.component.html',
  styleUrl: './users.component.scss'
})
export class UsersComponent
  implements OnInit {

  private readonly formBuilder =
    inject(FormBuilder);

  private readonly userService =
    inject(UserService);

  private readonly branchService =
    inject(BranchService);

  private readonly authService =
    inject(AuthService);

  private readonly toastService =
    inject(ToastService);

  private readonly router =
    inject(Router);

  private readonly cdr =
    inject(ChangeDetectorRef);

  readonly isAdmin =
    this.authService.hasAnyRole(Roles.Admin);

  readonly currentUserId =
    this.authService.getCurrentUser()?.userId ?? null;

  users: UserResponse[] = [];
  branches: BranchResponse[] = [];

  isLoading = true;
  isSaving = false;
  showInactive = false;
  isFormOpen = false;
  editingId: string | null = null;

  resettingPasswordForId: string | null = null;
  newPassword = '';
  isResettingPassword = false;

  errorMessage = '';

  readonly form = this.formBuilder.nonNullable.group({
    username: ['', [Validators.required, Validators.maxLength(100)]],
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.email]],
    password: [''],
    role: ['Cashier', [Validators.required]],
    branchId: [''],
    isActive: [true]
  });

  ngOnInit(): void {
    if (!this.isAdmin) {
      this.router.navigateByUrl('/open-shift');
      return;
    }

    this.loadBranches();
    this.loadUsers();
  }

  loadBranches(): void {
    this.branchService.getAll().subscribe({
      next: branches => {
        this.branches = branches;
        this.safeDetectChanges();
      }
    });
  }

  loadUsers(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.userService
      .getAll(this.showInactive)
      .pipe(
        finalize(() => {
          this.isLoading = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: users => {
          this.users = users;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تحميل المستخدمين';
        }
      });
  }

  toggleShowInactive(): void {
    this.showInactive = !this.showInactive;
    this.loadUsers();
  }

  branchName(branchId: string | null): string {
    if (!branchId) {
      return 'كل الفروع';
    }

    return this.branches.find(b => b.id === branchId)?.name ?? '—';
  }

  isSelf(user: UserResponse): boolean {
    return user.id === this.currentUserId;
  }

  openCreateForm(): void {
    this.editingId = null;

    this.form.reset({
      username: '',
      displayName: '',
      email: '',
      password: '',
      role: 'Cashier',
      branchId: '',
      isActive: true
    });

    this.form.controls.username.enable();
    this.form.controls.password.enable();
    this.form.controls.password.addValidators([
      Validators.required,
      Validators.minLength(6)
    ]);

    this.isFormOpen = true;
    this.errorMessage = '';
  }

  openEditForm(user: UserResponse): void {
    this.editingId = user.id;

    this.form.reset({
      username: user.username,
      displayName: user.displayName,
      email: user.email ?? '',
      password: '',
      role: user.role,
      branchId: user.branchId ?? '',
      isActive: user.isActive
    });

    // Username can't be changed after creation, and password changes
    // go through the separate "reset password" action below.
    this.form.controls.username.disable();
    this.form.controls.password.disable();
    this.form.controls.password.clearValidators();
    this.form.controls.password.updateValueAndValidity();

    this.isFormOpen = true;
    this.errorMessage = '';
  }

  cancelForm(): void {
    this.isFormOpen = false;
    this.editingId = null;
  }

  save(): void {
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isSaving = true;

    const request$ = this.editingId
      ? this.userService.update(this.editingId, {
          displayName: value.displayName,
          email: value.email?.trim() ? value.email.trim() : null,
          role: value.role,
          branchId: value.branchId || null,
          isActive: value.isActive
        })
      : this.userService.create({
          username: value.username,
          displayName: value.displayName,
          email: value.email?.trim() ? value.email.trim() : null,
          password: value.password,
          role: value.role,
          branchId: value.branchId || null
        });

    request$
      .pipe(
        finalize(() => {
          this.isSaving = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: () => {
          this.toastService.success(
            this.editingId
              ? 'تم تحديث المستخدم بنجاح'
              : 'تم إضافة المستخدم بنجاح'
          );

          this.isFormOpen = false;
          this.editingId = null;
          this.loadUsers();
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر حفظ المستخدم';
        }
      });
  }

  openResetPassword(user: UserResponse): void {
    this.resettingPasswordForId = user.id;
    this.newPassword = '';
    this.errorMessage = '';
  }

  cancelResetPassword(): void {
    this.resettingPasswordForId = null;
    this.newPassword = '';
  }

  submitResetPassword(): void {
    if (!this.resettingPasswordForId) {
      return;
    }

    if (this.newPassword.trim().length < 6) {
      this.errorMessage = 'كلمة المرور يجب ألا تقل عن 6 أحرف';
      return;
    }

    this.isResettingPassword = true;

    this.userService
      .resetPassword(this.resettingPasswordForId, {
        newPassword: this.newPassword.trim()
      })
      .pipe(
        finalize(() => {
          this.isResettingPassword = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: () => {
          this.toastService.success('تم تغيير كلمة المرور بنجاح');
          this.resettingPasswordForId = null;
          this.newPassword = '';
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تغيير كلمة المرور';
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
