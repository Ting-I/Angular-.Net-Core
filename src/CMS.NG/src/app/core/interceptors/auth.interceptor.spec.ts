import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';

import { environment } from '@env';
import {
  authInterceptor,
  FORBIDDEN_FALLBACK,
  FORBIDDEN_SUMMARY,
  SERVER_ERROR_FALLBACK,
  SERVER_ERROR_SUMMARY,
} from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('authInterceptor', () => {
  const apiUrl = `${environment.apiUrl}/app-roles`;
  const loginUrl = `${environment.apiUrl}/auth/login`;
  const profile = fakeProfile('helen', 'Helen Lin', ['Admin']);

  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;
  let messages: jasmine.Spy;

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
        // The real root-injector provider from app.config.ts; App renders the <p-toast /> that
        // shows what the interceptor adds to it.
        MessageService,
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    messages = spyOn(TestBed.inject(MessageService), 'add');
  }

  /** The single message the interceptor added, as PrimeNG would have received it. */
  const toast = () => messages.calls.mostRecent().args[0] as Record<string, string>;

  /** The ProblemDetails the API's exception middleware answers a 500 with. */
  const serverProblem = {
    type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1',
    title: '系統發生錯誤，請稍後再試。',
    status: 500,
    detail: 'An unexpected error occurred.',
    instance: '/api/app-roles',
    traceId: '0HNF2R9K7QM3T:00000004',
  };

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
    // The redirect is the whole message; a toast on top of it would follow the operator to Login.
    expect(messages).not.toHaveBeenCalled();
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

  // ---------- A 5xx is reported once, here ----------

  /** Fails `url` with `status` and `body`, and satisfies the caller's error handler. */
  function failWith(status: number, body: string | object, url = apiUrl): void {
    http.get(url).subscribe({ error: () => undefined });
    httpMock.expectOne(url).flush(body, { status, statusText: 'Server Error' });
  }

  it('toasts the safe message the API sent on a 500', () => {
    configure(true);

    failWith(500, serverProblem);

    expect(messages).toHaveBeenCalledTimes(1);
    expect(toast()['severity']).toBe('error');
    expect(toast()['summary']).toBe(SERVER_ERROR_SUMMARY);
    expect(toast()['detail']).toBe(serverProblem.title);
  });

  it('shows nothing the API kept to itself', () => {
    configure(true);

    failWith(500, serverProblem);

    // Whatever the server logged stays there — the toast can only ever say what the body carried.
    const shown = Object.values(toast()).join(' ');
    for (const secret of ['SELECT', 'at CMS.API', 'stack', 'server=', 'Exception']) {
      expect(shown.toLowerCase()).not.toContain(secret.toLowerCase());
    }
  });

  it('falls back to its own wording when the 5xx carried no message', () => {
    configure(true);

    // A 502 from a proxy in front of the API answers with an HTML page, not ProblemDetails.
    failWith(502, '<html>Bad Gateway</html>');

    expect(toast()['detail']).toBe(SERVER_ERROR_FALLBACK);
  });

  it('uses the English detail when the body carries no title', () => {
    configure(true);

    failWith(500, { status: 500, detail: 'An unexpected error occurred.' });

    expect(toast()['detail']).toBe('An unexpected error occurred.');
  });

  it('still reports the 500 to the caller', () => {
    configure(true);

    let status = 0;
    http.get(apiUrl).subscribe({ error: (error: { status: number }) => (status = error.status) });
    httpMock.expectOne(apiUrl).flush(serverProblem, { status: 500, statusText: 'Server Error' });

    expect(status).toBe(500);
  });

  it('toasts a 500 from the login call too', () => {
    configure(false);

    // Unlike the 401, there is nothing about a broken server the Login page can say better.
    failWith(500, serverProblem, loginUrl);

    expect(messages).toHaveBeenCalledTimes(1);
  });

  it('leaves a validation error for the form to render', () => {
    configure(true);

    failWith(400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { Topic: ['主題為必填。'] },
    });

    expect(messages).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('leaves a 404 and a 409 to the page that asked', () => {
    configure(true);

    failWith(404, '');
    failWith(409, { title: '原廠仍被使用', status: 409 });

    expect(messages).not.toHaveBeenCalled();
  });

  it('says nothing when the request never reached the server', () => {
    configure(true);

    // status 0: there is no server message to show, so nothing here changes.
    http.get(apiUrl).subscribe({ error: () => undefined });
    httpMock.expectOne(apiUrl).error(new ProgressEvent('error'));

    expect(messages).not.toHaveBeenCalled();
  });

  it('leaves a 5xx from another host alone', () => {
    configure(true);

    failWith(500, serverProblem, 'https://example.test/thing');

    expect(messages).not.toHaveBeenCalled();
  });

  // ---------- 403: the session is fine, this operator may not ----------

  /** What the API answers when an administrator resets their own password. */
  const forbiddenProblem = {
    title: '無法重設自己的密碼，請使用變更密碼',
    status: 403,
    detail: 'An operator cannot reset their own password; use POST /api/auth/change-password.',
  };

  it('toasts the reason the API refused on a 403', () => {
    configure(true);

    failWith(403, forbiddenProblem);

    expect(messages).toHaveBeenCalledTimes(1);
    expect(toast()['severity']).toBe('warn');
    expect(toast()['summary']).toBe(FORBIDDEN_SUMMARY);
    expect(toast()['detail']).toBe(forbiddenProblem.title);
  });

  it('falls back to its own wording when the 403 carried no message', () => {
    configure(true);

    // The authorization middleware refuses a missing role with an empty body.
    failWith(403, '');

    expect(toast()['detail']).toBe(FORBIDDEN_FALLBACK);
  });

  it('leaves the session alone on a 403', () => {
    configure(true);

    failWith(403, forbiddenProblem);

    // Unlike a 401 there is nothing wrong with the token — bouncing to /login would be a lie.
    expect(sessionStorage.getItem('auth-profile')).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('still reports the 403 to the caller', () => {
    configure(true);

    let status = 0;
    http.get(apiUrl).subscribe({ error: (error: HttpErrorResponse) => (status = error.status) });
    httpMock.expectOne(apiUrl).flush(forbiddenProblem, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
  });

  it('leaves a 403 from another host alone', () => {
    configure(true);

    failWith(403, forbiddenProblem, 'https://example.test/thing');

    expect(messages).not.toHaveBeenCalled();
  });
});
