import { inject } from '@angular/core';

import {
  HttpErrorResponse,
  HttpInterceptorFn
} from '@angular/common/http';

import { Router } from '@angular/router';

import {
  Observable,
  catchError,
  finalize,
  shareReplay,
  switchMap,
  throwError
} from 'rxjs';

import { AuthService } from './auth.service';
import { LoginResponse } from './auth.models';
import { environment } from '../../../environments/environment';

// Shared across all requests so that several 401s firing at once
// trigger a single /api/auth/refresh call instead of a stampede
// (the backend refresh token is single-use/rotating, so a second
// concurrent refresh with the same token would otherwise fail).
let refreshInProgress$: Observable<LoginResponse> | null = null;

export const authInterceptor:
  HttpInterceptorFn = (request, next) => {

  const router = inject(Router);
  const authService = inject(AuthService);

  // Every service in the app calls relative paths like '/api/products'.
  // If environment.apiBaseUrl is set (frontend and API on different
  // domains), rewrite it here in one place instead of every service.
  const targetRequest =
    environment.apiBaseUrl && request.url.startsWith('/api/')
      ? request.clone({ url: environment.apiBaseUrl + request.url })
      : request;

  const token = authService.getAccessToken();

  const authenticatedRequest = token
    ? targetRequest.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      })
    : targetRequest;

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthEndpoint = request.url.includes('/api/auth/');

      if (
        error.status !== 401 ||
        isAuthEndpoint ||
        !authService.getRefreshToken()
      ) {
        if (error.status === 401) {
          authService.logout();
          router.navigateByUrl('/login');
        }

        return throwError(() => error);
      }

      if (!refreshInProgress$) {
        refreshInProgress$ = authService.refreshAccessToken().pipe(
          shareReplay(1),
          finalize(() => {
            refreshInProgress$ = null;
          })
        );
      }

      return refreshInProgress$.pipe(
        switchMap(response => {
          const retriedRequest = targetRequest.clone({
            setHeaders: {
              Authorization: `Bearer ${response.accessToken}`
            }
          });

          return next(retriedRequest);
        }),

        catchError(refreshError => {
          authService.logout();
          router.navigateByUrl('/login');

          return throwError(() => refreshError);
        })
      );
    })
  );
};
