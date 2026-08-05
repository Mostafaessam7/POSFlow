import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  CreateUserRequest,
  ResetPasswordRequest,
  UpdateUserRequest,
  UserResponse
} from './user.models';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly http = inject(HttpClient);

  getAll(
    includeInactive = false
  ): Observable<UserResponse[]> {
    const params = new HttpParams()
      .set('includeInactive', includeInactive);

    return this.http.get<UserResponse[]>(
      '/api/users',
      { params }
    );
  }

  create(
    request: CreateUserRequest
  ): Observable<UserResponse> {
    return this.http.post<UserResponse>(
      '/api/users',
      request
    );
  }

  update(
    id: string,
    request: UpdateUserRequest
  ): Observable<UserResponse> {
    return this.http.put<UserResponse>(
      `/api/users/${id}`,
      request
    );
  }

  resetPassword(
    id: string,
    request: ResetPasswordRequest
  ): Observable<void> {
    return this.http.post<void>(
      `/api/users/${id}/reset-password`,
      request
    );
  }
}
