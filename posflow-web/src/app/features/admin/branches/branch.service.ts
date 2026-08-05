import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  BranchResponse,
  CreateBranchRequest,
  UpdateBranchRequest
} from './branch.models';

@Injectable({
  providedIn: 'root'
})
export class BranchService {
  private readonly http = inject(HttpClient);

  getAll(): Observable<BranchResponse[]> {
    return this.http.get<BranchResponse[]>(
      '/api/branches'
    );
  }

  create(
    request: CreateBranchRequest
  ): Observable<BranchResponse> {
    return this.http.post<BranchResponse>(
      '/api/branches',
      request
    );
  }

  update(
    id: string,
    request: UpdateBranchRequest
  ): Observable<BranchResponse> {
    return this.http.put<BranchResponse>(
      `/api/branches/${id}`,
      request
    );
  }
}
