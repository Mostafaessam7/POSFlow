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
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';
import { CategoryResponse, ProductResponse } from '../product.models';
import { ProductService } from '../product.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TranslatePipe
  ],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss'
})
export class ProductListComponent
  implements OnInit {

  private readonly formBuilder =
    inject(FormBuilder);

  private readonly productService =
    inject(ProductService);

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

  readonly canManage =
    this.authService.hasAnyRole(Roles.Admin, Roles.Manager);

  products: ProductResponse[] = [];
  categories: CategoryResponse[] = [];

  page = 1;
  pageSize = 20;
  totalPages = 1;
  totalCount = 0;

  isLoading = true;
  isSaving = false;
  showInactive = false;
  categoryFilter: string | null = null;
  isFormOpen = false;
  editingId: string | null = null;
  editingIsActive = true;
  editingRowVersion = '';

  newCategoryName = '';
  isAddingCategory = false;

  errorMessage = '';

  readonly form = this.formBuilder.nonNullable.group({
    nameAr: ['', [Validators.required, Validators.maxLength(250)]],
    nameEn: [''],
    barcode: [''],
    price: [0, [Validators.required, Validators.min(0.01)]],
    categoryId: [''],
    trackStock: [false],
    stockQuantity: [0, [Validators.min(0)]]
  });

  ngOnInit(): void {
    this.loadCategories();
    this.loadProducts();
  }

  loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: categories => {
        this.categories = categories;
        this.safeDetectChanges();
      }
    });
  }

  loadProducts(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.productService
      .getAll(
        this.showInactive,
        this.categoryFilter,
        this.page,
        this.pageSize
      )
      .pipe(
        finalize(() => {
          this.isLoading = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: result => {
          this.products = result.items;
          this.totalPages = result.totalPages;
          this.totalCount = result.totalCount;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تحميل المنتجات';
        }
      });
  }

  toggleShowInactive(): void {
    this.showInactive = !this.showInactive;
    this.page = 1;
    this.loadProducts();
  }

  onCategoryFilterChange(categoryId: string): void {
    this.categoryFilter = categoryId || null;
    this.page = 1;
    this.loadProducts();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) {
      return;
    }

    this.page = page;
    this.loadProducts();
  }

  addCategory(): void {
    const name = this.newCategoryName.trim();

    if (!name) {
      return;
    }

    this.isAddingCategory = true;

    this.productService
      .createCategory({ nameAr: name, nameEn: null })
      .pipe(
        finalize(() => {
          this.isAddingCategory = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: category => {
          this.categories = [...this.categories, category];
          this.newCategoryName = '';
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر إضافة التصنيف';
        }
      });
  }

  openCreateForm(): void {
    this.editingId = null;
    this.editingRowVersion = '';

    this.form.reset({
      nameAr: '',
      nameEn: '',
      barcode: '',
      price: 0,
      categoryId: '',
      trackStock: false,
      stockQuantity: 0
    });

    this.isFormOpen = true;
    this.errorMessage = '';
  }

  openEditForm(product: ProductResponse): void {
    this.editingId = product.id;
    this.editingIsActive = product.isActive;
    this.editingRowVersion = product.rowVersion;

    this.form.reset({
      nameAr: product.nameAr,
      nameEn: product.nameEn ?? '',
      barcode: product.barcode ?? '',
      price: product.price,
      categoryId: product.categoryId ?? '',
      trackStock: product.trackStock,
      stockQuantity: product.stockQuantity
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

    const payload = {
      nameAr: value.nameAr,
      nameEn: value.nameEn?.trim() ? value.nameEn.trim() : null,
      barcode: value.barcode?.trim() ? value.barcode.trim() : null,
      price: value.price,
      categoryId: value.categoryId || null,
      trackStock: value.trackStock,
      stockQuantity: value.stockQuantity
    };

    this.isSaving = true;

    const request$ = this.editingId
      ? this.productService.update(this.editingId, {
          ...payload,
          isActive: this.editingIsActive,
          rowVersion: this.editingRowVersion
        })
      : this.productService.create(payload);

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
              ? 'تم تحديث المنتج بنجاح'
              : 'تم إضافة المنتج بنجاح'
          );

          this.isFormOpen = false;
          this.editingId = null;
          this.loadProducts();
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر حفظ المنتج';
        }
      });
  }

  async deactivate(product: ProductResponse): Promise<void> {
    const confirmed = await this.confirmDialog.confirm(
      this.translationService.t('هل تريد إيقاف المنتج') +
        ` "${product.nameAr}"؟`,
      { danger: true, confirmLabel: 'إيقاف' }
    );

    if (!confirmed) {
      return;
    }

    this.productService
      .deactivate(product.id)
      .subscribe({
        next: () => {
          this.toastService.success('تم إيقاف المنتج');
          this.loadProducts();
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر إيقاف المنتج';
          this.safeDetectChanges();
        }
      });
  }

  goToPos(): void {
    this.router.navigateByUrl('/pos');
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
