import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { AuthService } from './auth.service';
import { AuthProfile } from '@core/models/auth.model';
import { fakeJwt, fakeProfile } from '@core/testing/fake-jwt';

describe('AuthService', () => {
  const loginUrl = `${environment.apiUrl}/auth/login`;
  let httpMock: HttpTestingController;

  /** The service reads storage when it is constructed, so seeding happens before injection. */
  const service = () => TestBed.inject(AuthService);

  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
    localStorage.clear();
  });

  it('starts signed out when session storage is empty', () => {
    expect(service().isAuthenticated()).toBeFalse();
    expect(service().accessToken()).toBeNull();
    expect(service().roles()).toEqual([]);
  });

  it('posts the credentials to /api/auth/login', () => {
    service().login({ userId: 'helen', password: 'Uwa@2026' }).subscribe();

    const request = httpMock.expectOne(loginUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userId: 'helen', password: 'Uwa@2026' });
    request.flush(fakeProfile('helen', 'Helen Lin'));
  });

  it('stores the profile in session storage, not local storage', () => {
    const profile = fakeProfile('helen', 'Helen Lin', ['Admin']);

    service().login({ userId: 'helen', password: 'Uwa@2026' }).subscribe();
    httpMock.expectOne(loginUrl).flush(profile);

    expect(JSON.parse(sessionStorage.getItem('auth-profile')!) as AuthProfile).toEqual(profile);
    expect(localStorage.getItem('auth-profile')).toBeNull();
    expect(service().isAuthenticated()).toBeTrue();
    expect(service().userName()).toBe('Helen Lin');
  });

  it('stores nothing when the login is rejected', () => {
    service()
      .login({ userId: 'helen', password: 'wrong' })
      .subscribe({ error: () => undefined });

    httpMock.expectOne(loginUrl).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('auth-profile')).toBeNull();
    expect(service().isAuthenticated()).toBeFalse();
  });

  it('reads a stored session back on construction', () => {
    sessionStorage.setItem('auth-profile', JSON.stringify(fakeProfile('miles', 'Miles Sun')));

    expect(service().isAuthenticated()).toBeTrue();
    expect(service().userName()).toBe('Miles Sun');
  });

  it('treats unparseable storage as signed out', () => {
    sessionStorage.setItem('auth-profile', 'not json');

    expect(service().isAuthenticated()).toBeFalse();
  });

  // ---------- Roles come out of the token ----------

  it('reads the roles from the token claims', () => {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify(fakeProfile('helen', 'Helen Lin', ['Admin', 'Editor'])),
    );

    expect(service().roles()).toEqual(['Admin', 'Editor']);
    expect(service().isAdmin()).toBeTrue();
  });

  it('accepts a single role, which the token carries as a bare string', () => {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify({
        userId: 'helen',
        userName: 'Helen Lin',
        accessToken: fakeJwt({ userId: 'helen', role: 'Editor' }),
      }),
    );

    expect(service().roles()).toEqual(['Editor']);
    expect(service().isAdmin()).toBeFalse();
  });

  it('reports no roles for a token that carries none', () => {
    sessionStorage.setItem('auth-profile', JSON.stringify(fakeProfile('miles', 'Miles Sun')));

    expect(service().roles()).toEqual([]);
    expect(service().isAdmin()).toBeFalse();
  });

  it('reports no roles for a token it cannot decode', () => {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify({ userId: 'x', userName: 'X', accessToken: 'not.a.jwt' }),
    );

    expect(service().roles()).toEqual([]);
  });

  // ---------- Logout ----------

  it('clears session storage on logout', () => {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify(fakeProfile('helen', 'Helen Lin', ['Admin'])),
    );
    const auth = service();

    auth.logout();

    expect(sessionStorage.getItem('auth-profile')).toBeNull();
    expect(auth.isAuthenticated()).toBeFalse();
    expect(auth.roles()).toEqual([]);
  });
});
