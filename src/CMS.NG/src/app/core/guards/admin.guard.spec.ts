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

import {
  ADMIN_LANDING_ROUTE,
  DEFAULT_LANDING_ROUTE,
  adminGuard,
  landingRedirect,
} from './admin.guard';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('adminGuard', () => {
  /** The guard reads neither, but CanActivateChildFn is handed both. */
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/app-users' } as RouterStateSnapshot;

  function signIn(roles: string[]): void {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify(fakeProfile('helen', 'Helen Lin', roles)),
    );

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  }

  const run = () => TestBed.runInInjectionContext(() => adminGuard(route, state));
  const url = (tree: UrlTree) => TestBed.inject(Router).serializeUrl(tree);

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('lets an operator holding the Admin role through', () => {
    signIn(['Admin']);

    expect(run()).toBeTrue();
  });

  it('lets an operator holding Admin among other roles through', () => {
    signIn(['Editor', 'Admin']);

    expect(run()).toBeTrue();
  });

  it('sends an operator without the Admin role to a page they can use', () => {
    signIn(['Editor']);

    const result = run();

    expect(result instanceof UrlTree).toBeTrue();
    expect(url(result as UrlTree)).toBe(DEFAULT_LANDING_ROUTE);
  });

  it('sends an operator with no roles at all away', () => {
    signIn([]);

    expect(run() instanceof UrlTree).toBeTrue();
  });

  it('is case sensitive on the role, as the API is on AppRole.RoleId', () => {
    signIn(['admin']);

    expect(run() instanceof UrlTree).toBeTrue();
  });
});

describe('landingRedirect', () => {
  function signIn(roles: string[]): void {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify(fakeProfile('helen', 'Helen Lin', roles)),
    );

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  }

  const run = () => TestBed.runInInjectionContext(() => landingRedirect());
  const url = (tree: UrlTree) => TestBed.inject(Router).serializeUrl(tree);

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('lands an administrator on 角色 AppRole, as before', () => {
    signIn(['Admin']);

    expect(url(run())).toBe(ADMIN_LANDING_ROUTE);
  });

  it('lands everybody else somewhere the API will answer', () => {
    // The regression this exists to prevent: 角色 AppRole was the landing page for every operator,
    // and the API now refuses it to anyone but an administrator.
    signIn(['Editor']);

    expect(url(run())).toBe(DEFAULT_LANDING_ROUTE);
  });
});
