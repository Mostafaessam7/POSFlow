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

import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/auth/roles';
import { ToastService } from '../../../shared/toast/toast.service';
import { BranchResponse } from './branch.models';
import { BranchService } from './branch.service';

@Component({
  selector: 'app-branches',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  templateUrl: './branches.component.html',
  styleUrl: './branches.component.scss'
})
export class BranchesComponent
  implements OnInit {

  private readonly formBuilder =
    inject(FormBuilder);

  private readonly branchService =
    inject(BranchService);

  private readonly authService =
    inject(AuthService);

  private readonly toastService =
    inject(ToastService);

  private readonly router =
    inject(Router);

  readonly isAdmin =
    this.authService.hasAnyRole(Roles.Admin);

  branches: BranchResponse[] = [];
  isLoading = true;
  isSaving = false;
  isFormOpen = false;
  editingId: string | null = null;

  errorMessage = '';

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    code: ['', [Validators.required, Validators.maxLength(50)]],
    isActive: [true]
  });

  ngOnInit(): void {
    if (!this.isAdmin) {
      this.router.navigateByUrl('/open-shift');
      return;
    }

    this.loadBranches();
  }

  loadBranches(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.branchService
      .getAll()
      .pipe(
        finalize(() => {
          this.isLoading = false;
        })
      )
      .subscribe({
        next: branches => {
          this.branches = branches;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تحميل الفروع';
        }
      });
  }

  openCreateForm(): void {
    this.editingId = null;

    this.form.reset({
      name: '',
      code: '',
      isActive: true
    });

    this.isFormOpen = true;
    this.errorMessage = '';
  }

  openEditForm(branch: BranchResponse): void {
    this.editingId = branch.id;

    this.form.reset({
      name: branch.name,
      code: branch.code,
      isActive: branch.isActive
    });

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
      ? this.branchService.update(this.editingId, value)
      : this.branchService.create({
          name: value.name,
          code: value.code
        });

    request$
      .pipe(
        finalize(() => {
          this.isSaving = false;
        })
      )
      .subscribe({
        next: () => {
          this.toastService.success(
            this.editingId
              ? 'تم تحديث الفرع بنجاح'
              : 'تم إضافة الفرع بنجاح'
          );

          this.isFormOpen = false;
          this.editingId = null;
          this.loadBranches();
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر حفظ الفرع';
        }
      });
  }

  goBack(): void {
    this.router.navigateByUrl('/open-shift');
  }
}
