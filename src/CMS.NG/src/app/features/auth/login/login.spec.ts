import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { environment } from '@env';
import { Login } from './login';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('Login', () => {
  const loginUrl = `${environment.apiUrl}/auth/login`;
  const profile = fakeProfile('helen', 'Helen Lin', ['Admin']);

  let fixture: ComponentFixture<Login>;
  let httpMock: HttpTestingController;
  let router: Router;

  const api = () => fixture.componentInstance as unknown as Record<string, any>;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimationsAsync(),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function fillIn(userId: string, password: string): void {
    api()['form'].setValue({ userId, password });
  }

  it('sends nothing until both fields are filled in', () => {
    api()['signIn']();

    httpMock.expectNone(loginUrl);
    expect(api()['form'].controls.userId.touched).toBeTrue();
  });

  it('posts { userId, password } to /api/auth/login', () => {
    fillIn('  helen  ', 'Uwa@2026');
    api()['signIn']();

    const request = httpMock.expectOne(loginUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userId: 'helen', password: 'Uwa@2026' });
    request.flush(profile);
  });

  it('stores the profile in session storage and leaves the Login page', () => {
    fillIn('helen', 'Uwa@2026');
    api()['signIn']();
    httpMock.expectOne(loginUrl).flush(profile);

    expect(sessionStorage.getItem('auth-profile')).toEqual(JSON.stringify(profile));
    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('reports one generic message for a rejected sign-in', () => {
    fillIn('helen', 'wrong');
    api()['signIn']();
    httpMock.expectOne(loginUrl).flush('', { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    // The API cannot say which of the three failures it was, and neither does this.
    expect(api()['error']()).toBe('帳號或密碼錯誤。');
    expect(fixture.nativeElement.textContent).toContain('帳號或密碼錯誤');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(sessionStorage.getItem('auth-profile')).toBeNull();
  });

  it('distinguishes a server failure from a rejected sign-in', () => {
    fillIn('helen', 'Uwa@2026');
    api()['signIn']();
    httpMock.expectOne(loginUrl).flush('', { status: 500, statusText: 'Server Error' });

    expect(api()['error']()).toBe('無法登入，請稍後再試。');
  });

  it('clears the password but keeps the 使用者代碼 after a failure', () => {
    fillIn('helen', 'wrong');
    api()['signIn']();
    httpMock.expectOne(loginUrl).flush('', { status: 401, statusText: 'Unauthorized' });

    expect(api()['form'].getRawValue()).toEqual({ userId: 'helen', password: '' });
    expect(api()['signingIn']()).toBeFalse();
  });
});

/**
 * The notice the page shows when the operator did not choose to be here. Its own describe, because
 * the reason arrives as a query parameter that `Login` reads once at construction — the
 * ActivatedRoute stub has to be in place before the component exists.
 */
describe('Login (returned after a password change)', () => {
  let fixture: ComponentFixture<Login>;
  let httpMock: HttpTestingController;

  const el = (testId: string): HTMLElement =>
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);

  function createWithReason(reason?: string): void {
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideAnimationsAsync(),
        {
          // Provided after provideRouter, so this is the ActivatedRoute the component injects.
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(reason ? { reason } : {}) },
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
  }

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('explains the sign-out when it was a password change that caused it', () => {
    createWithReason('password-changed');

    expect(el('login-notice').textContent).toContain('密碼已變更，請使用新密碼重新登入。');
  });

  it('shows nothing when the operator simply came to log in', () => {
    createWithReason();

    expect(el('login-notice')).toBeNull();
  });

  it('shows nothing for a reason it does not recognise', () => {
    createWithReason('something-else');

    expect(el('login-notice')).toBeNull();
  });

  it('drops the notice on the next sign-in attempt, so it cannot sit above a fresh error', () => {
    createWithReason('password-changed');
    expect(el('login-notice')).not.toBeNull();

    (fixture.componentInstance as unknown as Record<string, any>)['form'].setValue({
      userId: 'helen',
      password: 'wrong',
    });
    (fixture.componentInstance as unknown as Record<string, any>)['signIn']();

    httpMock
      .expectOne(`${environment.apiUrl}/auth/login`)
      .flush('', { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(el('login-notice')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('帳號或密碼錯誤');
  });
});
