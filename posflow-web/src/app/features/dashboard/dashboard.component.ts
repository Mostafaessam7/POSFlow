import {
  ChangeDetectorRef,
  Component,
  OnInit,
  inject
} from '@angular/core';

import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { Roles } from '../../core/auth/roles';
import { DailySummaryResponse } from './report.models';
import { ReportService } from './report.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent
  implements OnInit {

  private readonly reportService =
    inject(ReportService);

  private readonly authService =
    inject(AuthService);

  private readonly router =
    inject(Router);

  private readonly cdr =
    inject(ChangeDetectorRef);

  readonly canView =
    this.authService.hasAnyRole(Roles.Admin, Roles.Manager);

  summary: DailySummaryResponse | null = null;
  isLoading = true;
  errorMessage = '';

  ngOnInit(): void {
    if (!this.canView) {
      this.router.navigateByUrl('/open-shift');
      return;
    }

    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.reportService
      .getDailySummary()
      .pipe(
        finalize(() => {
          this.isLoading = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: summary => {
          this.summary = summary;
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر تحميل ملخص المبيعات';
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
