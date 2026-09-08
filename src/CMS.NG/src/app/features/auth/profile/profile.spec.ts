import { ComponentFixture, TestBed } from '@angular/core/testing';
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
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        MessageService,
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
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
});
