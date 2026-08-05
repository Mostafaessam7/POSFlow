import {
  inject
} from '@angular/core';

import {
  CanActivateFn,
  Router
} from '@angular/router';

import {
  AuthService
} from './auth.service';

import { Roles } from './roles';

export const adminGuard:
  CanActivateFn = () => {

  const authService =
    inject(AuthService);

  const router =
    inject(Router);

  if (authService.hasAnyRole(Roles.Admin)) {
    return true;
  }

  return router.createUrlTree([
    '/open-shift'
  ]);
};
