import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { AppUserDetail } from './app-user-detail';
import { AppUser } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';

describe('AppUserDetail', () => {
  let fixture: ComponentFixture<AppUserDetail>;
  let component: AppUserDetail;
  let httpMock: HttpTestingController;

  const userUrl = `${environment.apiUrl}/app-users/helen`;
  const resetUrl = `${environment.apiUrl}/app-users/helen/reset-password`;
  const rolesUrl = `${environment.apiUrl}/lookups/app-roles`;

  const user: AppUser = {
    pkid: 1,
    userId: 'helen',
    userName: 'Helen Lin',
    isActive: true,
    passwordUpdatedTime: '2026-01-15T04:00:00',
    roleCount: 2,
    roleIds: ['Admin', 'Editor'],
  };

  const roles: AppRoleLookup[] = [
    { roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1 },
    { roleId: 'Editor', roleName: 'Editor', permissionLevel: 50 },
    { roleId: 'User', roleName: 'User', permissionLevel: 100 },
  ];

  const api = () => component as unknown as Record<string, any>;

  async function setup(userId: string | null = 'helen'): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [AppUserDetail],
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

    fixture = TestBed.createComponent(AppUserDetail);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    // The 異動紀錄 badge this page renders fetches its own trail. That is the badge's own spec's
    // business, not this one's, so the request is answered here instead of in every case.
    httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]));
    httpMock.verify();
  });

  /** ngOnInit fetches the record and the role lookup in parallel. */
  function init(): void {
    fixture.detectChanges();
    httpMock.expectOne(userUrl).flush(user);
    httpMock.expectOne(rolesUrl).flush(roles);
    fixture.detectChanges();
  }

  it('loads the user and the role lookup in parallel', async () => {
    await setup();
    init();

    expect(api()['user']()).toEqual(user);
    expect(api()['loading']()).toBeFalse();
    expect(api()['notFound']()).toBeFalse();
  });

  it('renders the user fields including the UTC-corrected 密碼更新時間', async () => {
    await setup();
    init();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('檢視使用者');
    expect(text).toContain('Helen Lin');
    expect(text).toContain('2026/01/15');
  });

  it('resolves assigned roles to their display names', async () => {
    await setup();
    init();

    expect(api()['assignedRoles'].map((r: AppRoleLookup) => r.roleId)).toEqual(['Admin', 'Editor']);
    expect(fixture.nativeElement.textContent).toContain('Administrator (Admin)');
    expect(fixture.nativeElement.textContent).not.toContain('User (User)');
  });

  it('flags not found when the user request fails', async () => {
    await setup('helen');
    fixture.detectChanges();

    httpMock.expectOne(userUrl).flush('missing', { status: 404, statusText: 'Not Found' });
    httpMock.expectOne(rolesUrl).flush(roles);
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('查無此使用者');
  });

  it('does not call the API when the route has no id', async () => {
    await setup(null);
    fixture.detectChanges();

    httpMock.expectNone(rolesUrl);
    expect(api()['notFound']()).toBeTrue();
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates back to the list and to the edit page', async () => {
    await setup();
    init();

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['back']();
    expect(navigate).toHaveBeenCalledWith(['/app-users']);

    api()['edit']();
    expect(navigate).toHaveBeenCalledWith(['/app-users', 'helen', 'edit']);
  });

  it('resets the password on confirm and re-fetches so 密碼更新時間 refreshes', async () => {
    await setup();
    init();

    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmResetPassword']();

    const resetReq = httpMock.expectOne(resetUrl);
    expect(resetReq.request.method).toBe('POST');
    expect(resetReq.request.body).toEqual({});
    resetReq.flush(null);

    const refreshed = { ...user, passwordUpdatedTime: '2026-09-08T03:00:00' };
    httpMock.expectOne(userUrl).flush(refreshed);
    fixture.detectChanges();

    expect(api()['user']()).toEqual(refreshed);
    expect(api()['resetting']()).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('2026/09/08');
  });

  it('reports the missing default password when the reset returns 500', async () => {
    await setup();
    init();

    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmResetPassword']();

    httpMock
      .expectOne(resetUrl)
      .flush({ title: '系統設定缺少預設密碼' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(api()['resetting']()).toBeFalse();
    // The record is left as it was: no re-fetch is issued on the failure path.
    expect(api()['user']()).toEqual(user);
    expect(fixture.nativeElement.textContent).toContain('系統設定缺少預設密碼，無法重設。');
  });

  it('does not reset when the confirmation is dismissed', async () => {
    await setup();
    init();

    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake(() => confirmationService);

    api()['confirmResetPassword']();

    httpMock.expectNone(resetUrl);
  });
});
