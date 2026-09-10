import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, Routes, provideRouter } from '@angular/router';

import { adminGuard, landingRedirect } from './admin.guard';
import { authGuard } from './auth.guard';
import { fakeProfile } from '@core/testing/fake-jwt';

@Component({ standalone: true, template: '' })
class Stub {}

/**
 * `redirectTo` as a function, and `canActivateChild` on a path-less parent, exercised through the
 * real router rather than by calling either directly.
 *
 * The unit specs beside this one prove the two functions decide correctly when handed an injection
 * context. This proves the router *gives* them one: `landingRedirect` calls `inject(AuthService)`,
 * and if the router did not run it as an injection function that would throw on the app's very
 * first navigation — the one place a broken landing route is guaranteed to be noticed by everyone
 * and by no test.
 *
 * The routes are stubs on purpose. The shapes under test are the empty path, the `**` fallback and
 * the guarded parent; loading the real feature components would drag in PrimeNG and their HTTP
 * calls without saying anything more about any of the three.
 */
describe('landing redirect and admin guard, through the router', () => {
  const routes: Routes = [
    { path: 'login', component: Stub },
    {
      path: '',
      canActivateChild: [authGuard],
      children: [
        { path: '', pathMatch: 'full', redirectTo: landingRedirect },
        {
          path: '',
          canActivateChild: [adminGuard],
          children: [{ path: 'app-roles', component: Stub }],
        },
        { path: 'featured-promo-items', component: Stub },
        { path: '**', redirectTo: landingRedirect },
      ],
    },
  ];

  let router: Router;

  function signIn(roles: string[]): void {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify(fakeProfile('helen', 'Helen Lin', roles)),
    );

    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });

    router = TestBed.inject(Router);
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('lands an administrator on 角色 AppRole', async () => {
    signIn(['Admin']);

    await router.navigateByUrl('/');

    expect(router.url).toBe('/app-roles');
  });

  it('lands an operator without the role somewhere the API will answer', async () => {
    signIn(['Editor']);

    await router.navigateByUrl('/');

    expect(router.url).toBe('/featured-promo-items');
  });

  it('turns a typed admin URL into the page that operator can use', async () => {
    signIn(['Editor']);

    await router.navigateByUrl('/app-roles');

    expect(router.url).toBe('/featured-promo-items');
  });

  it('still lets an administrator reach a typed admin URL', async () => {
    signIn(['Admin']);

    await router.navigateByUrl('/app-roles');

    expect(router.url).toBe('/app-roles');
  });

  it('sends an unknown path through the same landing decision', async () => {
    signIn(['Editor']);

    await router.navigateByUrl('/no-such-page');

    expect(router.url).toBe('/featured-promo-items');
  });

  it('sends a signed-out visitor to Login before either runs', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    router = TestBed.inject(Router);

    await router.navigateByUrl('/app-roles');

    expect(router.url).toBe('/login');
  });
});
