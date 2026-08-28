import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';

import {
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  ResetPasswordWithTokenRequest
} from './auth.models';

import {
  clearAccessToken,
  getAccessToken,
  getCsrfToken,
  setAccessToken
} from './auth.session';

/**
 * Auth transport: access token in memory, refresh token in an HttpOnly cookie.
 *
 * This previously kept both tokens in localStorage. The access token being there was bad; the
 * refresh token being there was worse, because it is a renewable session — anything able to read it
 * could mint access tokens indefinitely. Neither is reachable from script now.
 *
 * `withCredentials: true` is required on every auth call so the browser sends and accepts the
 * cookie; without it the cookie is silently ignored and refresh fails with no obvious cause.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);

  // Non-sensitive display data (name, role, branch) — kept in localStorage purely so the shell can
  // render without waiting for a round trip. It is NOT a credential and is never trusted for
  // authorization: every guard checks it, and the server independently checks the token on every
  // request, so tampering with it changes what the UI draws and nothing the server honours.
  private readonly userKey = 'posflow_current_user';

  // Sent on auth calls to opt into cookie transport. Non-browser callers omit it and keep the
  // original body-based flow.
  private readonly cookieTransportHeaders = {
    'X-Auth-Transport': 'cookie'
  };

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/auth/login', request, {
        withCredentials: true,
        headers: this.cookieTransportHeaders
      })
      .pipe(tap(response => this.persistSession(response)));
  }

  refreshAccessToken(): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(
        '/api/auth/refresh',
        // Empty body on purpose: the refresh token travels in the cookie. Putting it in the body
        // is exactly what this transport exists to avoid.
        {},
        {
          withCredentials: true,
          headers: this.csrfHeaders()
        }
      )
      .pipe(tap(response => this.persistSession(response)));
  }

  /**
   * Re-establishes the session after a page reload, where the in-memory access token is gone but
   * the HttpOnly cookie survives. Resolves to null rather than erroring when there is no session,
   * so callers can treat "not logged in" as an ordinary outcome instead of a failure.
   */
  restoreSession(): Observable<LoginResponse | null> {
    return this.refreshAccessToken().pipe(
      catchError(() => {
        this.clearSession();

        return of(null);
      })
    );
  }

  getAccessToken(): string | null {
    return getAccessToken();
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
    return !!getAccessToken();
  }

  logout(): void {
    this.clearSession();

    // Best-effort server-side revoke and cookie clear. The local session is already gone, so the
    // UI is not blocked on this call.
    this.http
      .post('/api/auth/logout', {}, {
        withCredentials: true,
        headers: this.csrfHeaders()
      })
      .subscribe({ error: () => {} });
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>('/api/auth/forgot-password', request);
  }

  resetPasswordWithToken(request: ResetPasswordWithTokenRequest): Observable<void> {
    return this.http.post<void>('/api/auth/reset-password', request);
  }

  private csrfHeaders(): Record<string, string> {
    const csrf = getCsrfToken();

    return csrf
      ? { ...this.cookieTransportHeaders, 'X-XSRF-TOKEN': csrf }
      : { ...this.cookieTransportHeaders };
  }

  private persistSession(response: LoginResponse): void {
    if (response.accessToken) {
      setAccessToken(response.accessToken);
    }

    localStorage.setItem(this.userKey, JSON.stringify(response));
  }

  private clearSession(): void {
    clearAccessToken();
    localStorage.removeItem(this.userKey);

    // Removes the keys the previous localStorage-based implementation wrote, so a browser that
    // used the old build does not keep a stale refresh token sitting in Web Storage forever.
    localStorage.removeItem('posflow_access_token');
    localStorage.removeItem('posflow_refresh_token');
  }
}
