import { TestBed } from '@angular/core/testing';
import { UrlTree, provideRouter } from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  function setup(isLoggedIn: boolean): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { isLoggedIn: () => isLoggedIn }
        }
      ]
    });
  }

  it('allows navigation when the user is logged in', () => {
    setup(true);

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as any, {} as any)
    );

    expect(result).toBe(true);
  });

  it('redirects to /login when the user is not logged in', () => {
    setup(false);

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as any, {} as any)
    ) as UrlTree;

    expect(result instanceof UrlTree).toBe(true);
    expect(result.toString()).toBe('/login');
  });
});
