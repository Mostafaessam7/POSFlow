import {
  ApplicationConfig,
  provideZoneChangeDetection
} from '@angular/core';

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
    )
  ]
};