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

  // ---------- 個人資料 ----------

  describe('updateProfile', () => {
    const profileUrl = `${environment.apiUrl}/auth/profile`;

    function signIn(): AuthProfile {
      const profile = fakeProfile('helen', 'Helen Lin', ['Admin', 'Editor']);
      sessionStorage.setItem('auth-profile', JSON.stringify(profile));
      return profile;
    }

    it('PUTs the 使用者名稱 alone, with no key in the body', () => {
      signIn();

      service().updateProfile({ userName: 'Helen Chen' }).subscribe();

      const request = httpMock.expectOne(profileUrl);
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ userName: 'Helen Chen' });

      request.flush({ userId: 'helen', userName: 'Helen Chen', roleIds: ['Admin', 'Editor'] });
    });

    it('folds the new name into the live session and its storage', () => {
      const before = signIn();
      const auth = service();

      auth.updateProfile({ userName: 'Helen Chen' }).subscribe();
      httpMock
        .expectOne(profileUrl)
        .flush({ userId: 'helen', userName: 'Helen Chen', roleIds: ['Admin', 'Editor'] });

      expect(auth.userName()).toBe('Helen Chen');
      expect((JSON.parse(sessionStorage.getItem('auth-profile')!) as AuthProfile).userName).toBe(
        'Helen Chen',
      );

      // The key and the token ride through untouched — so the roles are unchanged too.
      expect(auth.profile()!.userId).toBe('helen');
      expect(auth.accessToken()).toBe(before.accessToken);
      expect(auth.roles()).toEqual(['Admin', 'Editor']);
    });

    it('changes nothing when the save is rejected', () => {
      signIn();
      const auth = service();

      auth.updateProfile({ userName: '' }).subscribe({ error: () => undefined });
      httpMock.expectOne(profileUrl).flush('', { status: 400, statusText: 'Bad Request' });

      expect(auth.userName()).toBe('Helen Lin');
      expect((JSON.parse(sessionStorage.getItem('auth-profile')!) as AuthProfile).userName).toBe(
        'Helen Lin',
      );
    });

    it('stores nothing when there is no session to update', () => {
      // A 401 clears the session before the response lands; there is then nothing to rename.
      const auth = service();

      auth.updateProfile({ userName: 'Helen Chen' }).subscribe();
      httpMock
        .expectOne(profileUrl)
        .flush({ userId: 'helen', userName: 'Helen Chen', roleIds: [] });

      expect(sessionStorage.getItem('auth-profile')).toBeNull();
      expect(auth.isAuthenticated()).toBeFalse();
    });
  });

  // ---------- syncUserName ----------

  describe('syncUserName', () => {
    function signIn(userId = 'helen'): void {
      sessionStorage.setItem(
        'auth-profile',
        JSON.stringify(fakeProfile(userId, 'Helen Lin', ['Admin'])),
      );
    }

    it('renames the session when the key is the signed-in one', () => {
      signIn();
      const auth = service();

      auth.syncUserName('helen', '林海倫');

      expect(auth.userName()).toBe('林海倫');
      expect((JSON.parse(sessionStorage.getItem('auth-profile')!) as AuthProfile).userName).toBe(
        '林海倫',
      );
    });

    it('matches the key case-insensitively, as SQL Server does', () => {
      signIn('Helen');
      const auth = service();

      auth.syncUserName('HELEN', '林海倫');

      expect(auth.userName()).toBe('林海倫');
    });

    it('is a no-op for anybody else', () => {
      signIn();
      const auth = service();

      auth.syncUserName('miles', 'Miles Sun');

      expect(auth.userName()).toBe('Helen Lin');
    });

    it('is a no-op when nobody is signed in', () => {
      const auth = service();

      auth.syncUserName('helen', '林海倫');

      expect(sessionStorage.getItem('auth-profile')).toBeNull();
      expect(auth.isAuthenticated()).toBeFalse();
    });
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

  // ---------- 變更密碼 ----------

  describe('changePassword', () => {
    const changePasswordUrl = `${environment.apiUrl}/auth/change-password`;

    const request = {
      currentPassword: 'Uwa@2026',
      newPassword: 'N3wPass!word',
      confirmNewPassword: 'N3wPass!word',
    };

    function signIn(): AuthProfile {
      const profile = fakeProfile('helen', 'Helen Lin', ['Admin', 'Editor']);
      sessionStorage.setItem('auth-profile', JSON.stringify(profile));
      return profile;
    }

    it('POSTs the three passwords, with no key in the body', () => {
      signIn();

      service().changePassword(request).subscribe();

      const pending = httpMock.expectOne(changePasswordUrl);
      expect(pending.request.method).toBe('POST');
      expect(pending.request.body).toEqual(request);

      pending.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('drops the session on success, token and all', () => {
      signIn();
      const auth = service();

      auth.changePassword(request).subscribe();
      httpMock
        .expectOne(changePasswordUrl)
        .flush(null, { status: 204, statusText: 'No Content' });

      // The token was signed for a password that no longer exists, and the API will honour it for
      // its full 24 hours regardless — so it is dropped here rather than left to expire.
      expect(sessionStorage.getItem('auth-profile')).toBeNull();
      expect(auth.isAuthenticated()).toBeFalse();
      expect(auth.accessToken()).toBeNull();
      expect(auth.userName()).toBe('');
      expect(auth.roles()).toEqual([]);
    });

    it('does not clear the session before the API has answered', () => {
      signIn();
      const auth = service();

      auth.changePassword(request).subscribe();

      // The request is on the wire and unanswered; the interceptor still needs the token on it.
      const pending = httpMock.expectOne(changePasswordUrl);
      expect(auth.isAuthenticated()).toBeTrue();

      pending.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('does not sign the operator out when the change is refused', () => {
      signIn();
      const auth = service();

      auth.changePassword(request).subscribe({ error: () => undefined });
      httpMock
        .expectOne(changePasswordUrl)
        .flush({ status: 400, title: '目前密碼錯誤' }, { status: 400, statusText: 'Bad Request' });

      expect(auth.isAuthenticated()).toBeTrue();
    });
  });
});
