import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { AppRoleForm } from './app-role-form';
import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user-lookup.model';

describe('AppRoleForm', () => {
  let fixture: ComponentFixture<AppRoleForm>;
  let component: AppRoleForm;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/app-roles`;
  const usersUrl = `${environment.apiUrl}/lookups/app-users`;

  const users: AppUserLookup[] = [
    { userId: 'helen', userName: 'helen', isActive: true },
    { userId: 'miles', userName: 'Miles Sun', isActive: true },
  ];

  const role: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 1,
    userIds: ['helen'],
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(roleId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [AppRoleForm],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(roleId ? { id: roleId } : {}) },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleForm);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Runs ngOnInit and satisfies the initial lookup (+ record) requests. */
  function init(roleId: string | null): void {
    fixture.detectChanges();
    httpMock.expectOne(usersUrl).flush(users);
    if (roleId) {
      httpMock.expectOne(`${baseUrl}/${roleId}`).flush(role);
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

    it('starts in add mode with the schema default permission level', () => {
      init(null);

      expect(api()['isEdit']()).toBeFalse();
      expect(component['form'].getRawValue().permissionLevel).toBe(100);
      expect(fixture.nativeElement.textContent).toContain('新增角色');
    });

    it('leaves the role code editable', () => {
      init(null);

      expect(component['form'].controls.roleId.enabled).toBeTrue();
    });

    it('marks the form invalid until the required fields are filled', () => {
      init(null);

      expect(component['form'].invalid).toBeTrue();

      component['form'].patchValue({ roleId: 'Editor', roleName: 'Editor' });
      expect(component['form'].valid).toBeTrue();
    });

    it('does not POST when the form is invalid', () => {
      init(null);

      api()['save']();

      httpMock.expectNone(baseUrl);
      expect(component['form'].controls.roleId.touched).toBeTrue();
    });

    it('POSTs a trimmed request and navigates to the new record', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      component['form'].patchValue({
        roleId: '  Editor  ',
        roleName: '  Editor  ',
        permissionLevel: 50,
        description: '  內容編輯  ',
        userIds: ['helen'],
      });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        roleId: 'Editor',
        roleName: 'Editor',
        permissionLevel: 50,
        description: '內容編輯',
        userIds: ['helen'],
      });
      req.flush({ ...role, roleId: 'Editor', roleName: 'Editor' });

      expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Editor']);
      expect(api()['saving']()).toBeFalse();
    });

    it('sends a null description when the field is left blank', () => {
      init(null);

      component['form'].patchValue({ roleId: 'Editor', roleName: 'Editor', description: '   ' });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.body.description).toBeNull();
      req.flush(role);
    });

    it('stops saving and stays on the form when the role code conflicts', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      component['form'].patchValue({ roleId: 'Admin', roleName: 'Dup' });
      api()['save']();

      httpMock
        .expectOne(baseUrl)
        .flush({ title: '角色代碼已存在' }, { status: 409, statusText: 'Conflict' });

      expect(api()['saving']()).toBeFalse();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('builds multiselect options from the user lookup', () => {
      init(null);

      expect(api()['userOptions']).toEqual([
        { value: 'helen', label: 'helen (helen)' },
        { value: 'miles', label: 'Miles Sun (miles)' },
      ]);
    });

    it('navigates back to the list on cancel', () => {
      init(null);
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      api()['cancel']();

      expect(navigate).toHaveBeenCalledWith(['/app-roles']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('Admin');
    });

    it('loads the record and patches the form', () => {
      init('Admin');

      expect(api()['isEdit']()).toBeTrue();
      expect(component['form'].getRawValue()).toEqual({
        roleId: 'Admin',
        roleName: 'Administrator',
        permissionLevel: 1,
        description: '系統管理員',
        userIds: ['helen'],
      });
      expect(fixture.nativeElement.textContent).toContain('編輯角色');
    });

    it('disables the role code because it is the primary key', () => {
      init('Admin');

      expect(component['form'].controls.roleId.disabled).toBeTrue();
    });

    it('PUTs the whole record including the disabled key', () => {
      init('Admin');
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      component['form'].patchValue({
        roleName: '系統管理者',
        permissionLevel: 2,
        userIds: ['helen', 'miles'],
      });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        roleId: 'Admin',
        roleName: '系統管理者',
        permissionLevel: 2,
        description: '系統管理員',
        userIds: ['helen', 'miles'],
      });
      req.flush({ ...role, roleName: '系統管理者' });

      expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin']);
    });

    it('recovers when the record cannot be loaded', () => {
      fixture.detectChanges();
      httpMock.expectOne(usersUrl).flush(users);
      httpMock
        .expectOne(`${baseUrl}/Admin`)
        .flush('missing', { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(api()['loading']()).toBeFalse();
      expect(component['form'].getRawValue().roleName).toBe('');
    });
  });
});
