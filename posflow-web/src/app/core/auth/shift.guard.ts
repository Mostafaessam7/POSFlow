import { inject } from '@angular/core';

import {
  CanActivateFn,
  Router
} from '@angular/router';

import {
  catchError,
  map,
  of
} from 'rxjs';

import {
  ShiftService
} from '../../features/shifts/shift.service';

export const shiftGuard:
  CanActivateFn = () => {

  const shiftService =
    inject(ShiftService);

  const router =
    inject(Router);

return shiftService.getCurrent().pipe(
  map(response => {
    if (response.hasOpenShift && response.shift) {
      return true;
    }

    return router.createUrlTree([
      '/open-shift'
    ]);
  }),

  catchError(() => {
    return of(
      router.createUrlTree([
        '/open-shift'
      ])
    );
  })
);
};