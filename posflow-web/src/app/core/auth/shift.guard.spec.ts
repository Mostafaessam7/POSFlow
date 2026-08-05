import { TestBed } from '@angular/core/testing';
import { UrlTree, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';

import { shiftGuard } from './shift.guard';
import { ShiftService } from '../../features/shifts/shift.service';
import { CurrentShiftResponse } from '../../features/shifts/shift.models';

describe('shiftGuard', () => {
  function setup(getCurrent: () => Observable<CurrentShiftResponse>): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ShiftService, useValue: { getCurrent } }
      ]
    });
  }

  it('allows navigation when there is an open shift', done => {
    setup(() =>
      of({
        hasOpenShift: true,
        shift: {
          id: 's1',
          tenantId: 't1',
          branchId: 'b1',
          userId: 'u1',
          openingCash: 0,
          closingCash: null,
          cashSales: 0,
          expectedCash: null,
          cashDifference: null,
          openedAtUtc: new Date().toISOString(),
          closedAtUtc: null,
          status: 'Open'
        }
      })
    );

    const result$ = TestBed.runInInjectionContext(() =>
      shiftGuard({} as any, {} as any)
    ) as Observable<boolean | UrlTree>;

    result$.subscribe(value => {
      expect(value).toBe(true);
      done();
    });
  });

  it('redirects to /open-shift when there is no open shift', done => {
    setup(() => of({ hasOpenShift: false, shift: null }));

    const result$ = TestBed.runInInjectionContext(() =>
      shiftGuard({} as any, {} as any)
    ) as Observable<boolean | UrlTree>;

    result$.subscribe(value => {
      expect(value instanceof UrlTree).toBe(true);
      expect((value as UrlTree).toString()).toBe('/open-shift');
      done();
    });
  });

  it('redirects to /open-shift when the shift check request fails', done => {
    setup(() => throwError(() => new Error('network error')));

    const result$ = TestBed.runInInjectionContext(() =>
      shiftGuard({} as any, {} as any)
    ) as Observable<boolean | UrlTree>;

    result$.subscribe(value => {
      expect(value instanceof UrlTree).toBe(true);
      expect((value as UrlTree).toString()).toBe('/open-shift');
      done();
    });
  });
});
