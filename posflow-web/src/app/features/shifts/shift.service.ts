import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { PagedResult } from '../../shared/paged-result.model';

import {
  CloseShiftRequest,
  CurrentShiftResponse,
  OpenShiftRequest,
  ShiftResponse
} from './shift.models';

@Injectable({
  providedIn: 'root'
})
export class ShiftService {
  private readonly http = inject(HttpClient);

  getCurrent(): Observable<CurrentShiftResponse> {
    return this.http.get<CurrentShiftResponse>(
      '/api/shifts/current'
    );
  }

  getHistory(
    page = 1,
    pageSize = 30
  ): Observable<PagedResult<ShiftResponse>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedResult<ShiftResponse>>(
      '/api/shifts/history',
      { params }
    );
  }

  getBranchHistory(
    page = 1,
    pageSize = 50
  ): Observable<PagedResult<ShiftResponse>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<PagedResult<ShiftResponse>>(
      '/api/shifts/branch-history',
      { params }
    );
  }

  open(
    request: OpenShiftRequest
  ): Observable<ShiftResponse> {
    return this.http.post<ShiftResponse>(
      '/api/shifts/open',
      request
    );
  }

  close(
    shiftId: string,
    request: CloseShiftRequest
  ): Observable<ShiftResponse> {
    return this.http.post<ShiftResponse>(
      `/api/shifts/${shiftId}/close`,
      request
    );
  }
}
