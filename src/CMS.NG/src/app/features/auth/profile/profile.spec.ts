import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MessageService } from 'primeng/api';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { Profile } from './profile';
import { AuthService } from '@core/services/auth.service';
import { AuthProfile, UserProfile } from '@core/models/auth.model';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('Profile', () => {
  const profileUrl = `${environment.apiUrl}/auth/profile`;

  let fixture: ComponentFixture<Profile>;
  let httpMock: HttpTestingController;
  let router: Router;

  /** Reaches the component's protected members without widening its real API. */
  const api = () => fixture.componentInstance as unknown as Record<string, any>;

  const el = (testId: string): HTMLElement =>
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);

  /** AuthService reads storage when it is constructed, so the session is seeded before injection. */
  function signIn(userId = 'helen', userName = 'Helen Lin', roles: string[] = ['Admin', 'Editor']) {
    sessionStorage.setItem('auth-profile', JSON.stringify(fakeProfile(userId, userName, roles)));
  }

  function createComponent(): void {
    TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        MessageService,
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);

    // A successful password change navigates away; the spy keeps the test in this component.
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
  }

  const storedProfile = (): AuthProfile =>
    JSON.parse(sessionStorage.getItem('auth-profile')!) as AuthProfile;

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ---------- What the page shows ----------

  it('shows the signed-in 使用者代碼 and does not let it be edited', () => {
    signIn();
    createComponent();

    const userId = el('profile-user-id') as HTMLInputElement;
    expect(userId.value).toBe('helen');
    expect(userId.disabled).toBeTrue();

    // Disabled, so the key is not even part of the form's value — only getRawValue() sees it.
    expect(api()['form'].value.userId).toBeUndefined();
    expect(api()['form'].getRawValue().userId).toBe('helen');
  });

  it('shows the 使用者名稱 from the session in an editable control', () => {
    signIn();
    createComponent();

    const userName = el('profile-user-name') as HTMLInputElement;
    expect(userName.value).toBe('Helen Lin');
    expect(userName.disabled).toBeFalse();
  });

  it('renders the roles from the token as read-only tags', () => {
    signIn('helen', 'Helen Lin', ['Admin', 'Editor']);
    createComponent();

    const roles = el('profile-roles');
    expect(roles.textContent).toContain('Admin');
    expect(roles.textContent).toContain('Editor');

    // Display only: nothing on the page can change them.
    expect(roles.querySelector('input')).toBeNull();
    expect(roles.querySelector('button')).toBeNull();
    expect(api()['form'].getRawValue().roleIds).toBeUndefined();
  });

  it('says so when the operator holds no roles', () => {
    signIn('miles', 'Miles Sun', []);
    createComponent();

    expect(el('profile-roles').textContent).toContain('尚未指派角色');
  });

  // ---------- Saving ----------

  it('PUTs only the 使用者名稱 to /api/auth/profile', () => {
    signIn();
    createComponent();

    api()['form'].patchValue({ userName: 'Helen Chen' });
    api()['save']();

    const request = httpMock.expectOne(profileUrl);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ userName: 'Helen Chen' });

    request.flush({ userId: 'helen', userName: 'Helen Chen', roleIds: ['Admin', 'Editor'] });
  });

  it('trims the 使用者名稱 before sending it', () => {
    signIn();
    createComponent();

    api()['form'].patchValue({ userName: '  Helen Chen  ' });
    api()['save']();

    const request = httpMock.expectOne(profileUrl);
    expect(request.request.body).toEqual({ userName: 'Helen Chen' });

    request.flush({ userId: 'helen', userName: 'Helen Chen', roleIds: [] });
  });

  it('updates the name the app shell reads, and the stored session with it', () => {
    signIn();
    createComponent();
    const auth = TestBed.inject(AuthService);

    expect(auth.userName()).toBe('Helen Lin');

    api()['form'].patchValue({ userName: 'Helen Chen' });
    api()['save']();

    const updated: UserProfile = { userId: 'helen', userName: 'Helen Chen', roleIds: ['Admin'] };
    httpMock.expectOne(profileUrl).flush(updated);

    // What the top bar binds to...
    expect(auth.userName()).toBe('Helen Chen');
    // ...and what survives a reload.
    expect(storedProfile().userName).toBe('Helen Chen');
    // The key and the token are untouched.
    expect(storedProfile().userId).toBe('helen');
    expect(storedProfile().accessToken).toBe(
      fakeProfile('helen', 'Helen Lin', ['Admin', 'Editor']).accessToken,
    );
  });

  it('toasts 已儲存 with the saved name', () => {
    signIn();
    createComponent();
    // The component provides its own MessageService, so the spy has to go on that instance.
    const messageService = fixture.debugElement.injector.get(MessageService);
    spyOn(messageService, 'add');

    api()['form'].patchValue({ userName: 'Helen Chen' });
    api()['save']();
    httpMock.expectOne(profileUrl).flush({ userId: 'helen', userName: 'Helen Chen', roleIds: [] });

    expect(messageService.add).toHaveBeenCalledWith({
      severity: 'success',
      summary: '已儲存',
      detail: 'Helen Chen',
    });
  });

  it('sends nothing and shows the required message for an empty 使用者名稱', () => {
    signIn();
    createComponent();

    api()['form'].patchValue({ userName: '' });
    api()['save']();
    fixture.detectChanges();

    httpMock.expectNone(profileUrl);
    expect(fixture.nativeElement.textContent).toContain('使用者名稱為必填');
    expect(TestBed.inject(AuthService).userName()).toBe('Helen Lin');
  });

  it('sends nothing for a 使用者名稱 of whitespace alone', () => {
    signIn();
    createComponent();

    api()['form'].patchValue({ userName: '    ' });
    api()['save']();
    fixture.detectChanges();

    httpMock.expectNone(profileUrl);
    expect(fixture.nativeElement.textContent).toContain('使用者名稱為必填');
  });

  it('leaves the session alone and toasts the failure when the save is rejected', () => {
    signIn();
    createComponent();
    // The component provides its own MessageService, so the spy has to go on that instance.
    const messageService = fixture.debugElement.injector.get(MessageService);
    spyOn(messageService, 'add');

    api()['form'].patchValue({ userName: 'Helen Chen' });
    api()['save']();
    httpMock.expectOne(profileUrl).flush('', { status: 400, statusText: 'Bad Request' });

    expect(TestBed.inject(AuthService).userName()).toBe('Helen Lin');
    expect(storedProfile().userName).toBe('Helen Lin');
    expect(messageService.add).toHaveBeenCalledWith({
      severity: 'error',
      summary: '儲存失敗',
      detail: '使用者名稱為必填。',
    });
  });

  it('restores the stored name when the edit is discarded', () => {
    signIn();
    createComponent();

    api()['form'].patchValue({ userName: 'Half typed' });
    api()['reset']();

    expect(api()['form'].getRawValue().userName).toBe('Helen Lin');
  });

  // ---------- 變更密碼 Change Password ----------

  describe('change password', () => {
    const changePasswordUrl = `${environment.apiUrl}/auth/change-password`;

    /** A new password that clears the rule: 12 characters over all four classes. */
    const strong = 'N3wPass!word';

    function fillPasswords(
      currentPassword: string,
      newPassword: string,
      confirmNewPassword = newPassword,
    ): void {
      api()['passwordForm'].setValue({ currentPassword, newPassword, confirmNewPassword });
    }

    /** Submits, then renders whatever validation the submit turned on. */
    function submit(): void {
      api()['changePassword']();
      fixture.detectChanges();
    }

    function flushNoContent(): void {
      httpMock.expectOne(changePasswordUrl).flush(null, { status: 204, statusText: 'No Content' });
    }

    // ---------- What travels ----------

    it('POSTs the three plaintext fields to /api/auth/change-password', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', strong);
      submit();

      const request = httpMock.expectOne(changePasswordUrl);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({
        currentPassword: 'Uwa@2026',
        newPassword: strong,
        confirmNewPassword: strong,
      });

      request.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('sends no key and no hash of any kind', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', strong);
      submit();

      const request = httpMock.expectOne(changePasswordUrl);
      const body = request.request.body as Record<string, unknown>;

      // The account comes from the token, so the body cannot name one.
      expect(Object.keys(body).sort()).toEqual([
        'confirmNewPassword',
        'currentPassword',
        'newPassword',
      ]);

      // Hashing is the server's job — a 64-character hex string here would mean the browser did it.
      for (const value of Object.values(body)) {
        expect(value).not.toMatch(/^[0-9a-f]{64}$/i);
      }

      request.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('does not trim the passwords it sends', () => {
      signIn();
      createComponent();

      // Whitespace is part of a password; trimming it would hash something else.
      fillPasswords(' Uwa@2026 ', ' N3wPass!word ');
      submit();

      // expectOne consumes the request, so the TestRequest is held and flushed rather than
      // matched a second time.
      const request = httpMock.expectOne(changePasswordUrl);
      const body = request.request.body as Record<string, string>;
      expect(body['currentPassword']).toBe(' Uwa@2026 ');
      expect(body['newPassword']).toBe(' N3wPass!word ');

      request.flush(null, { status: 204, statusText: 'No Content' });
    });

    // ---------- Client validation: length ----------

    it('sends nothing and shows the rule for a new password shorter than 8', () => {
      signIn();
      createComponent();

      // Four classes but seven characters: the length half of the rule is what fails.
      fillPasswords('Uwa@2026', 'Abc123!');
      submit();

      httpMock.expectNone(changePasswordUrl);
      expect(el('new-password-rule').textContent).toContain('密碼長度至少需 8 碼');
      expect(el('new-password-rule').classList.contains('field-error')).toBeTrue();
    });

    // ---------- Client validation: character classes ----------

    for (const weak of ['abcdefgh', 'ABCDEFGH', '12345678', '!!!!!!!!', 'Abcdefgh', 'abcdefg1']) {
      it(`sends nothing for "${weak}" — fewer than 3 of the 4 classes`, () => {
        signIn();
        createComponent();

        fillPasswords('Uwa@2026', weak);
        submit();

        httpMock.expectNone(changePasswordUrl);
        expect(el('new-password-rule').classList.contains('field-error')).toBeTrue();
      });
    }

    for (const acceptable of ['Abcdefg1', 'Abcdefg!', 'ABCDEF1!', 'abcdef1!']) {
      it(`accepts "${acceptable}" — exactly 3 of the 4 classes`, () => {
        signIn();
        createComponent();

        fillPasswords('Uwa@2026', acceptable);
        submit();

        const request = httpMock.expectOne(changePasswordUrl);
        expect((request.request.body as Record<string, string>)['newPassword']).toBe(acceptable);

        request.flush(null, { status: 204, statusText: 'No Content' });
      });
    }

    it('counts a caseless script as the symbol class, as the server does', () => {
      signIn();
      createComponent();

      // 密碼 + abc + 123: three classes over eight characters.
      fillPasswords('Uwa@2026', '密碼abc123');
      submit();

      flushNoContent();
    });

    it('shows the rule as a bilingual hint before anything is typed', () => {
      signIn();
      createComponent();

      const rule = el('new-password-rule');
      expect(rule.classList.contains('field-hint')).toBeTrue();
      expect(rule.classList.contains('field-error')).toBeFalse();
      expect(rule.textContent).toContain('密碼長度至少需 8 碼');
      expect(rule.textContent).toContain('at least 3 of the 4 classes');
    });

    // ---------- Client validation: the confirmation ----------

    it('sends nothing and says so when the confirmation differs', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', strong, 'N3wPass!wordX');
      submit();

      httpMock.expectNone(changePasswordUrl);
      expect(el('confirm-password-error').textContent).toContain('新密碼與確認新密碼不一致');
    });

    it('treats a confirmation differing only in case as a mismatch', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', 'N3wPass!word', 'N3wpass!word');
      submit();

      httpMock.expectNone(changePasswordUrl);
      expect(el('confirm-password-error').textContent).toContain('新密碼與確認新密碼不一致');
    });

    it('stays quiet about the mismatch while the confirmation is still empty', () => {
      signIn();
      createComponent();

      api()['passwordForm'].patchValue({ currentPassword: 'Uwa@2026', newPassword: strong });
      api()['passwordForm'].controls.newPassword.markAsTouched();
      fixture.detectChanges();

      expect(el('confirm-password-error')).toBeNull();
    });

    // ---------- Client validation: required ----------

    it('sends nothing and marks all three fields when nothing has been typed', () => {
      signIn();
      createComponent();

      submit();

      httpMock.expectNone(changePasswordUrl);
      expect(el('current-password-error').textContent).toContain('目前密碼為必填');
      expect(el('new-password-error').textContent).toContain('新密碼為必填');
      expect(el('confirm-password-error').textContent).toContain('確認新密碼為必填');
    });

    it('sends nothing when only the current password is missing', () => {
      signIn();
      createComponent();

      fillPasswords('', strong);
      submit();

      httpMock.expectNone(changePasswordUrl);
      expect(el('current-password-error').textContent).toContain('目前密碼為必填');
    });

    // ---------- After the round trip ----------

    it('clears all three fields once the change is accepted', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', strong);
      submit();
      flushNoContent();

      expect(api()['passwordForm'].getRawValue()).toEqual({
        currentPassword: '',
        newPassword: '',
        confirmNewPassword: '',
      });
    });

    it('drops the session, so the old password no longer buys 24 hours of access', () => {
      signIn();
      createComponent();
      const auth = TestBed.inject(AuthService);
      expect(auth.isAuthenticated()).toBeTrue();

      fillPasswords('Uwa@2026', strong);
      submit();
      flushNoContent();

      // Nothing on the server takes the token back, so the client has to.
      expect(sessionStorage.getItem('auth-profile')).toBeNull();
      expect(auth.isAuthenticated()).toBeFalse();
      expect(auth.accessToken()).toBeNull();
      expect(auth.roles()).toEqual([]);
    });

    it('returns to 登入 with the reason, so the page can explain the sign-out', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', strong);
      submit();
      flushNoContent();

      expect(router.navigate).toHaveBeenCalledWith(['/login'], {
        queryParams: { reason: 'password-changed' },
      });
    });

    it('puts no password in the URL it navigates to', () => {
      signIn();
      createComponent();

      fillPasswords('Uwa@2026', strong);
      submit();
      flushNoContent();

      const navigation = JSON.stringify(
        (router.navigate as jasmine.Spy).calls.mostRecent().args,
      );
      expect(navigation).not.toContain(strong);
      expect(navigation).not.toContain('Uwa@2026');
    });

    it('does not toast, because the page is torn down by the navigation', () => {
      signIn();
      createComponent();
      // The component provides its own MessageService, so the spy has to go on that instance.
      const messageService = fixture.debugElement.injector.get(MessageService);
      spyOn(messageService, 'add');

      fillPasswords('Uwa@2026', strong);
      submit();
      flushNoContent();

      // A message the operator never sees is worse than none; the Login page carries it instead.
      expect(messageService.add).not.toHaveBeenCalled();
    });

    it('keeps the session when the change is refused', () => {
      signIn();
      createComponent();
      const before = storedProfile();

      fillPasswords('wrong-password', strong);
      submit();
      httpMock
        .expectOne(changePasswordUrl)
        .flush({ status: 400, title: '目前密碼錯誤' }, { status: 400, statusText: 'Bad Request' });

      // Nothing changed, so nothing is taken away — and the operator stays on the page to retry.
      expect(storedProfile()).toEqual(before);
      expect(TestBed.inject(AuthService).isAuthenticated()).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('shows the API 目前密碼錯誤 wording and keeps the fields for a retry', () => {
      signIn();
      createComponent();
      const messageService = fixture.debugElement.injector.get(MessageService);
      spyOn(messageService, 'add');

      fillPasswords('wrong-password', strong);
      submit();
      httpMock.expectOne(changePasswordUrl).flush(
        { status: 400, title: '目前密碼錯誤', detail: 'The current password does not match.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(messageService.add).toHaveBeenCalledWith({
        severity: 'error',
        summary: '變更失敗',
        detail: '目前密碼錯誤',
      });

      // Nothing is cleared, so the operator corrects the one field that was wrong.
      expect(api()['passwordForm'].getRawValue().newPassword).toBe(strong);
    });

    it('shows the API complexity wording when the server is the one that refuses', () => {
      signIn();
      createComponent();
      const messageService = fixture.debugElement.injector.get(MessageService);
      spyOn(messageService, 'add');

      const rule =
        '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號';

      fillPasswords('Uwa@2026', strong);
      submit();
      httpMock
        .expectOne(changePasswordUrl)
        .flush({ status: 400, title: rule }, { status: 400, statusText: 'Bad Request' });

      expect(messageService.add).toHaveBeenCalledWith({
        severity: 'error',
        summary: '變更失敗',
        detail: rule,
      });
    });

    it('falls back to a generic message when the failure carries no ProblemDetails', () => {
      signIn();
      createComponent();
      const messageService = fixture.debugElement.injector.get(MessageService);
      spyOn(messageService, 'add');

      fillPasswords('Uwa@2026', strong);
      submit();
      httpMock.expectOne(changePasswordUrl).flush('', { status: 500, statusText: 'Server Error' });

      expect(messageService.add).toHaveBeenCalledWith({
        severity: 'error',
        summary: '變更失敗',
        detail: '請稍後再試。',
      });
    });

    // ---------- The two forms stay separate ----------

    it('does not send a password when the 使用者名稱 is saved', () => {
      signIn();
      createComponent();

      api()['passwordForm'].patchValue({ currentPassword: 'Uwa@2026', newPassword: strong });
      api()['form'].patchValue({ userName: 'Helen Chen' });
      api()['save']();

      const request = httpMock.expectOne(profileUrl);
      expect(request.request.body).toEqual({ userName: 'Helen Chen' });

      request.flush({ userId: 'helen', userName: 'Helen Chen', roleIds: [] });
    });

    it('does not send a 使用者名稱 when the password is changed', () => {
      signIn();
      createComponent();

      api()['form'].patchValue({ userName: 'Helen Chen' });
      fillPasswords('Uwa@2026', strong);
      submit();

      const request = httpMock.expectOne(changePasswordUrl);
      expect((request.request.body as Record<string, unknown>)['userName']).toBeUndefined();
      httpMock.expectNone(profileUrl);

      request.flush(null, { status: 204, statusText: 'No Content' });
    });
  });
});
