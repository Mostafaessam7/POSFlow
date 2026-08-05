import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import {
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  ResetPasswordWithTokenRequest
} from './auth.models';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly tokenKey =
    'posflow_access_token';

  private readonly refreshTokenKey =
    'posflow_refresh_token';

  private readonly userKey =
    'posflow_current_user';

  login(
    request: LoginRequest
  ): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(
        '/api/auth/login',
        request
      )
      .pipe(
        tap(response => this.persistSession(response))
      );
  }

  refreshAccessToken(): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(
        '/api/auth/refresh',
        { refreshToken: this.getRefreshToken() }
      )
      .pipe(
        tap(response => this.persistSession(response))
      );
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.refreshTokenKey);
  }

  getCurrentUser(): LoginResponse | null {
    const raw = localStorage.getItem(this.userKey);

    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as LoginResponse;
    } catch {
      return null;
    }
  }

  hasAnyRole(...roles: string[]): boolean {
    const user = this.getCurrentUser();

    return !!user && roles.includes(user.role);
  }

  isLoggedIn(): boolean {
    return !!this.getAccessToken();
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();

    this.clearSession();

    if (refreshToken) {
      // Best-effort server-side revoke; local logout already happened
      // above so the UI isn't blocked on this network call.
      this.http
        .post('/api/auth/logout', { refreshToken })
        .subscribe({ error: () => {} });
    }
  }

  forgotPassword(
    request: ForgotPasswordRequest
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      '/api/auth/forgot-password',
      request
    );
  }

  resetPasswordWithToken(
    request: ResetPasswordWithTokenRequest
  ): Observable<void> {
    return this.http.post<void>(
      '/api/auth/reset-password',
      request
    );
  }

  private persistSession(response: LoginResponse): void {
    localStorage.setItem(this.tokenKey, response.accessToken);
    localStorage.setItem(this.refreshTokenKey, response.refreshToken);
    localStorage.setItem(this.userKey, JSON.stringify(response));
  }

  private clearSession(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.refreshTokenKey);
    localStorage.removeItem(this.userKey);
  }
}
