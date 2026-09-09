import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { AppUserForm } from './app-user-form';
import { AppUser } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';
import { AuthService } from '@core/services/auth.service';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('AppUserForm', () => {
  let fixture: ComponentFixture<AppUserForm>;
  let component: AppUserForm;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/app-users`;
  const rolesUrl = `${environment.apiUrl}/lookups/app-roles`;

  const roles: AppRoleLookup[] = [
    { roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1 },
    { roleId: 'Editor', roleName: 'Editor', permissionLevel: 50 },
  ];

  const user: AppUser = {
    pkid: 1,
    userId: 'helen',
    userName: 'Helen Lin',
    isActive: true,
    passwordUpdatedTime: '2026-01-15T04:00:00',
    roleCount: 1,
    roleIds: ['Admin'],
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(userId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [AppUserForm],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(userId ? { id: userId } : {}) },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserForm);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Runs ngOnInit and satisfies the initial lookup (+ record) requests. */
  function init(userId: string | null): void {
    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush(roles);
    if (userId) {
      httpMock.expectOne(`${baseUrl}/${userId}`).flush(user);
    }
    fixture.detectChanges();
  }

  afterEach(() => {
    // The 異動紀錄 badge this page renders fetches its own trail. That is the badge's own spec's
    // business, not this one's, so the request is answered here instead of in every case.
    httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]));
    httpMock.verify();
  });

  // ---------- Add mode ----------

  describe('add mode', () => {
    beforeEach(async () => {
      await setup(null);
    });

    it('starts in add mode with 啟用 defaulting to true', () => {
      init(null);

      expect(api()['isEdit']()).toBeFalse();
      expect(component['form'].getRawValue().isActive).toBeTrue();
      expect(fixture.nativeElement.textContent).toContain('新增使用者');
    });

    it('leaves the user code editable and explains the default password', () => {
      init(null);

      expect(component['form'].controls.userId.enabled).toBeTrue();
      expect(fixture.nativeElement.textContent).toContain('新帳號將套用系統預設密碼');
    });

    it('renders no password control at all', () => {
      init(null);

      expect(Object.keys(component['form'].controls).sort()).toEqual([
        'isActive',
        'roleIds',
        'userId',
        'userName',
      ]);
      expect(fixture.nativeElement.querySelector('input[type="password"]')).toBeNull();
    });

    it('marks the form invalid until the required fields are filled', () => {
      init(null);

      expect(component['form'].invalid).toBeTrue();

      component['form'].patchValue({ userId: 'miles', userName: 'Miles Sun' });
      expect(component['form'].valid).toBeTrue();
    });

    it('does not POST when the form is invalid', () => {
      init(null);

      api()['save']();

      httpMock.expectNone(baseUrl);
      expect(component['form'].controls.userId.touched).toBeTrue();
    });

    it('POSTs a trimmed request with no password key and navigates to the new record', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      component['form'].patchValue({
        userId: '  miles  ',
        userName: '  Miles Sun  ',
        isActive: false,
        roleIds: ['Editor'],
      });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        userId: 'miles',
        userName: 'Miles Sun',
        isActive: false,
        roleIds: ['Editor'],
      });
      req.flush({ ...user, userId: 'miles', userName: 'Miles Sun' });

      expect(navigate).toHaveBeenCalledWith(['/app-users', 'miles']);
      expect(api()['saving']()).toBeFalse();
    });

    it('builds multiselect options from the role lookup', () => {
      init(null);

      expect(api()['roleOptions']).toEqual([
        { value: 'Admin', label: 'Administrator (Admin)' },
        { value: 'Editor', label: 'Editor (Editor)' },
      ]);
    });

    it('stops saving and stays on the form when the user code conflicts', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      component['form'].patchValue({ userId: 'helen', userName: 'Dup' });
      api()['save']();

      httpMock
        .expectOne(baseUrl)
        .flush({ title: '使用者代碼已存在' }, { status: 409, statusText: 'Conflict' });
      fixture.detectChanges();

      expect(api()['saving']()).toBeFalse();
      expect(navigate).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('使用者代碼已存在。');
    });

    it('reports the missing default password when the create returns 500', () => {
      init(null);

      component['form'].patchValue({ userId: 'miles', userName: 'Miles Sun' });
      api()['save']();

      httpMock
        .expectOne(baseUrl)
        .flush({ title: '系統設定缺少預設密碼' }, { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(api()['saving']()).toBeFalse();
      expect(fixture.nativeElement.textContent).toContain('系統設定缺少預設密碼，無法建立帳號。');
    });

    it('navigates back to the list on cancel', () => {
      init(null);
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      api()['cancel']();

      expect(navigate).toHaveBeenCalledWith(['/app-users']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('helen');
    });

    it('loads the record and patches the form', () => {
      init('helen');

      expect(api()['isEdit']()).toBeTrue();
      expect(component['form'].getRawValue()).toEqual({
        userId: 'helen',
        userName: 'Helen Lin',
        isActive: true,
        roleIds: ['Admin'],
      });
      expect(fixture.nativeElement.textContent).toContain('編輯使用者');
    });

    it('disables the user code because it is the primary key', () => {
      init('helen');

      expect(component['form'].controls.userId.disabled).toBeTrue();
    });

    it('hides the default-password hint in edit mode', () => {
      init('helen');

      expect(fixture.nativeElement.textContent).not.toContain('新帳號將套用系統預設密碼');
    });

    it('PUTs the whole record including the disabled key and no password', () => {
      init('helen');
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      component['form'].patchValue({
        userName: '林海倫',
        isActive: false,
        roleIds: ['Admin', 'Editor'],
      });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        userId: 'helen',
        userName: '林海倫',
        isActive: false,
        roleIds: ['Admin', 'Editor'],
      });
      req.flush({ ...user, userName: '林海倫' });

      expect(navigate).toHaveBeenCalledWith(['/app-users', 'helen']);
    });

    it('recovers when the record cannot be loaded', () => {
      fixture.detectChanges();
      httpMock.expectOne(rolesUrl).flush(roles);
      httpMock
        .expectOne(`${baseUrl}/helen`)
        .flush('missing', { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(api()['loading']()).toBeFalse();
      expect(component['form'].getRawValue().userName).toBe('');
    });
  });

  // ---------- Renaming yourself from 使用者 AppUser ----------

  describe('when the edited account is the signed-in one', () => {
    /** AuthService reads storage when constructed, so the session is seeded before setup(). */
    function signIn(userId: string, userName: string): void {
      sessionStorage.setItem(
        'auth-profile',
        JSON.stringify(fakeProfile(userId, userName, ['Admin'])),
      );
    }

    beforeEach(() => sessionStorage.clear());
    afterEach(() => sessionStorage.clear());

    it('updates the name the app shell reads', async () => {
      signIn('helen', 'Helen Lin');
      await setup('helen');
      init('helen');
      const auth = TestBed.inject(AuthService);

      component['form'].patchValue({ userName: '林海倫' });
      api()['save']();
      httpMock.expectOne(baseUrl).flush({ ...user, userName: '林海倫' });

      // Without this the top bar would keep the login-time name until the next sign-in.
      expect(auth.userName()).toBe('林海倫');
      expect(
        (JSON.parse(sessionStorage.getItem('auth-profile')!) as { userName: string }).userName,
      ).toBe('林海倫');
    });

    it('matches the key case-insensitively, as the API does', async () => {
      signIn('HELEN', 'Helen Lin');
      await setup('helen');
      init('helen');

      component['form'].patchValue({ userName: '林海倫' });
      api()['save']();
      httpMock.expectOne(baseUrl).flush({ ...user, userName: '林海倫' });

      expect(TestBed.inject(AuthService).userName()).toBe('林海倫');
    });

    it('leaves the session alone when somebody else is edited', async () => {
      signIn('miles', 'Miles Sun');
      await setup('helen');
      init('helen');

      component['form'].patchValue({ userName: '林海倫' });
      api()['save']();
      httpMock.expectOne(baseUrl).flush({ ...user, userName: '林海倫' });

      expect(TestBed.inject(AuthService).userName()).toBe('Miles Sun');
    });
  });
});
