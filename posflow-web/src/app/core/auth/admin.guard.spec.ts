import { TestBed } from '@angular/core/testing';
import { UrlTree, provideRouter } from '@angular/router';

import { adminGuard } from './admin.guard';
import { AuthService } from './auth.service';

describe('adminGuard', () => {
  function setup(isAdmin: boolean): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            hasAnyRole: (...roles: string[]) =>
              isAdmin && roles.includes('Admin')
          }
        }
      ]
    });
  }

  it('allows navigation for an Admin', () => {
    setup(true);

    const result = TestBed.runInInjectionContext(() =>
      adminGuard({} as any, {} as any)
    );

    expect(result).toBe(true);
  });

  it('redirects non-admins to /open-shift', () => {
    setup(false);

    const result = TestBed.runInInjectionContext(() =>
      adminGuard({} as any, {} as any)
    ) as UrlTree;

    expect(result instanceof UrlTree).toBe(true);
    expect(result.toString()).toBe('/open-shift');
  });
});
