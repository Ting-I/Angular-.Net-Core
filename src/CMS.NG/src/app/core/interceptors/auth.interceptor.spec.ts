import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';

import { environment } from '@env';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('authInterceptor', () => {
  const apiUrl = `${environment.apiUrl}/app-roles`;
  const loginUrl = `${environment.apiUrl}/auth/login`;
  const profile = fakeProfile('helen', 'Helen Lin', ['Admin']);

  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;

  /** Seeds the session before anything is injected — AuthService reads storage when constructed. */
  function configure(signedIn: boolean): void {
    if (signedIn) {
      sessionStorage.setItem('auth-profile', JSON.stringify(profile));
    }

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ---------- Attaching the token ----------

  it('attaches the stored token as a Bearer header', () => {
    configure(true);

    http.get(apiUrl).subscribe();

    const request = httpMock.expectOne(apiUrl);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${profile.accessToken}`);
    request.flush([]);
  });

  it('attaches the token to writes as well as reads', () => {
    configure(true);

    http.post(apiUrl, { roleId: 'Admin' }).subscribe();

    const request = httpMock.expectOne(apiUrl);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${profile.accessToken}`);
    request.flush({});
  });

  it('sends no Authorization header when there is no token', () => {
    configure(false);

    http.get(apiUrl).subscribe();

    const request = httpMock.expectOne(apiUrl);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush([]);
  });

  it('leaves requests to other hosts alone', () => {
    configure(true);

    http.get('https://example.test/thing').subscribe();

    const request = httpMock.expectOne('https://example.test/thing');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('picks up the token issued by a login that happens mid-session', () => {
    configure(false);

    TestBed.inject(AuthService).login({ userId: 'helen', password: 'Uwa@2026' }).subscribe();
    httpMock.expectOne(loginUrl).flush(profile);

    http.get(apiUrl).subscribe();

    const request = httpMock.expectOne(apiUrl);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${profile.accessToken}`);
    request.flush([]);
  });

  // ---------- A 401 ends the session ----------

  it('clears session storage and redirects to the Login page on a 401', () => {
    configure(true);

    http.get(apiUrl).subscribe({ error: () => undefined });
    httpMock.expectOne(apiUrl).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('auth-profile')).toBeNull();
    expect(TestBed.inject(AuthService).isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('still reports the 401 to the caller', () => {
    configure(true);

    let status = 0;
    http.get(apiUrl).subscribe({ error: (error: { status: number }) => (status = error.status) });
    httpMock.expectOne(apiUrl).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
  });

  it('leaves the session alone on any other error', () => {
    configure(true);

    http.get(apiUrl).subscribe({ error: () => undefined });
    httpMock.expectOne(apiUrl).flush('', { status: 500, statusText: 'Server Error' });

    expect(sessionStorage.getItem('auth-profile')).toEqual(JSON.stringify(profile));
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does not redirect when it is the login call itself that answers 401', () => {
    configure(false);

    // The Login page reports 帳號或密碼錯誤 in place; bouncing to the page the operator is
    // already on would wipe that message.
    TestBed.inject(AuthService)
      .login({ userId: 'helen', password: 'wrong' })
      .subscribe({ error: () => undefined });

    httpMock.expectOne(loginUrl).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).not.toHaveBeenCalled();
  });
});
