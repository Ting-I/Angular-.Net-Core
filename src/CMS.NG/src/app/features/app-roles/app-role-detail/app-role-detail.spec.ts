import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { convertToParamMap } from '@angular/router';

import { environment } from '@env';
import { AppRoleDetail } from './app-role-detail';
import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user-lookup.model';

describe('AppRoleDetail', () => {
  let fixture: ComponentFixture<AppRoleDetail>;
  let component: AppRoleDetail;
  let httpMock: HttpTestingController;

  const roleUrl = `${environment.apiUrl}/app-roles/Admin`;
  const usersUrl = `${environment.apiUrl}/lookups/app-users`;

  const role: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 2,
    userIds: ['helen', 'miles'],
  };

  const users: AppUserLookup[] = [
    { userId: 'helen', userName: 'helen', isActive: true },
    { userId: 'miles', userName: 'Miles Sun', isActive: true },
    { userId: 'jenny', userName: 'Jenny Tsao', isActive: true },
  ];

  const api = () => component as unknown as Record<string, any>;

  async function setup(roleId: string | null = 'Admin'): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [AppRoleDetail],
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

    fixture = TestBed.createComponent(AppRoleDetail);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('loads the role and the user lookup in parallel', async () => {
    await setup();
    fixture.detectChanges();

    httpMock.expectOne(roleUrl).flush(role);
    httpMock.expectOne(usersUrl).flush(users);
    fixture.detectChanges();

    expect(api()['role']()).toEqual(role);
    expect(api()['loading']()).toBeFalse();
    expect(api()['notFound']()).toBeFalse();
  });

  it('renders the role fields', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(roleUrl).flush(role);
    httpMock.expectOne(usersUrl).flush(users);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
    expect(text).toContain('檢視角色');
  });

  it('resolves assigned users to their display names', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(roleUrl).flush(role);
    httpMock.expectOne(usersUrl).flush(users);
    fixture.detectChanges();

    expect(api()['assignedUsers'].map((u: AppUserLookup) => u.userId)).toEqual(['helen', 'miles']);
    expect(fixture.nativeElement.textContent).toContain('Miles Sun (miles)');
    expect(fixture.nativeElement.textContent).not.toContain('Jenny Tsao');
  });

  it('flags not found when the role request fails', async () => {
    await setup('Missing');
    fixture.detectChanges();

    httpMock
      .expectOne(`${environment.apiUrl}/app-roles/Missing`)
      .flush('missing', { status: 404, statusText: 'Not Found' });
    httpMock.expectOne(usersUrl).flush(users);
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('查無此角色');
  });

  it('does not call the API when the route has no id', async () => {
    await setup(null);
    fixture.detectChanges();

    httpMock.expectNone(usersUrl);
    expect(api()['notFound']()).toBeTrue();
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates back to the list and to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(roleUrl).flush(role);
    httpMock.expectOne(usersUrl).flush(users);

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['back']();
    expect(navigate).toHaveBeenCalledWith(['/app-roles']);

    api()['edit']();
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin', 'edit']);
  });
});
