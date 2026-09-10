import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { authGuard } from './auth.guard';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('authGuard', () => {
  /** The guard reads neither, but CanActivateChildFn is handed both. */
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/courses' } as RouterStateSnapshot;

  function configure(signedIn: boolean): void {
    if (signedIn) {
      sessionStorage.setItem('auth-profile', JSON.stringify(fakeProfile('helen', 'Helen Lin')));
    }

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  }

  const run = () => TestBed.runInInjectionContext(() => authGuard(route, state));

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('redirects to the Login page when session storage holds no token', () => {
    configure(false);

    const result = run();

    expect(result instanceof UrlTree).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('redirects when the stored value is not a usable profile', () => {
    sessionStorage.setItem('auth-profile', JSON.stringify({ userId: 'helen' }));
    configure(false);

    expect(run() instanceof UrlTree).toBeTrue();
  });

  it('lets the navigation through when a token is stored', () => {
    configure(true);

    expect(run()).toBeTrue();
  });
});
