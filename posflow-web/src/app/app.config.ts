import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideZoneChangeDetection
} from '@angular/core';

import { firstValueFrom } from 'rxjs';

import {
  provideRouter
} from '@angular/router';

import {
  provideHttpClient,
  withInterceptors,
  withXhr
} from '@angular/common/http';

import { routes } from './app.routes';

import {
  authInterceptor
} from './core/auth/auth.interceptor';

import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection(),

    provideRouter(routes),

    provideHttpClient(
      withInterceptors([
        authInterceptor
      ]),

      // provideHttpClient() defaults to a fetch()-based backend, which
      // the app's zone.js polyfill (angular.json's "polyfills") does
      // NOT patch - only XMLHttpRequest is. Without this, every HTTP
      // response lands correctly (confirmed live: the component's
      // state updated as expected) but nothing ever tells Angular to
      // re-render, so the view is stuck on whatever it last painted
      // (e.g. OpenShiftComponent staying on its loading state forever
      // even after the request succeeds). withXhr() switches back to
      // the XHR backend, which zone.js does patch, restoring automatic
      // change detection after every HTTP call.
      withXhr()
    ),

    // The access token lives in memory now, so a page reload loses it while the HttpOnly refresh
    // cookie survives. Without this the user would appear logged out after every F5 despite having
    // a valid session. One silent refresh at startup re-establishes it.
    //
    // provideAppInitializer runs before the first route resolves, so guards see the restored
    // session rather than racing it. Failure is swallowed by restoreSession(): "no valid cookie"
    // is the ordinary not-logged-in case, not an error worth blocking startup over.
    provideAppInitializer(() => {
      const authService = inject(AuthService);

      return firstValueFrom(authService.restoreSession());
    })
  ]
};