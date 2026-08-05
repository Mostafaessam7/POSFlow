import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { DailySummaryResponse } from './report.models';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private readonly http = inject(HttpClient);

  getDailySummary(): Observable<DailySummaryResponse> {
    return this.http.get<DailySummaryResponse>(
      '/api/reports/daily-summary'
    );
  }
}
