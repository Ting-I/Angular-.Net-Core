import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { AppUserList } from './app-user-list';
import { AppUser } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';

describe('AppUserList', () => {
  let fixture: ComponentFixture<AppUserList>;
  let component: AppUserList;
  let httpMock: HttpTestingController;

  const queryUrl = `${environment.apiUrl}/app-users/query`;
  const rolesUrl = `${environment.apiUrl}/lookups/app-roles`;

  const EMPTY_QUERY = {
    keyword: null,
    isActive: null,
    roleId: null,
    passwordUpdatedFrom: null,
    passwordUpdatedTo: null,
  };

  const users: AppUser[] = [
    {
      pkid: 1,
      userId: 'helen',
      userName: 'Helen Lin',
      isActive: true,
      passwordUpdatedTime: '2026-01-15T04:00:00',
      roleCount: 2,
      roleIds: [],
    },
    {
      pkid: 2,
      userId: 'retired',
      userName: 'Retired User',
      isActive: false,
      passwordUpdatedTime: null,
      roleCount: 0,
      roleIds: [],
    },
  ];

  const roles: AppRoleLookup[] = [
    { roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1 },
    { roleId: 'User', roleName: 'User', permissionLevel: 100 },
  ];

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  async function setup(queryParams: Record<string, string> = {}): Promise<void> {
    // A spec that needs different route params re-runs setup, so the module is reset first.
    TestBed.resetTestingModule();
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppUserList],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  beforeEach(async () => {
    await setup();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  /** ngOnInit loads the role lookup first, then issues the list query. */
  function initAndFlush(payload: AppUser[] = users): void {
    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush(roles);
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  it('loads the role lookup and then the user list on init', () => {
    initAndFlush();

    expect(component).toBeTruthy();
    expect(api()['roles']()).toEqual(roles);
    expect(api()['users']()).toEqual(users);
    expect(api()['loading']()).toBeFalse();
  });

  it('renders one row per user with the 啟用 icon and the formatted 密碼更新時間', () => {
    initAndFlush();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('helen');
    expect(rows[0].textContent).toContain('Helen Lin');
    expect(rows[0].querySelector('i.pi-check')).not.toBeNull();
    expect(rows[0].textContent).toContain('2026/01/15');

    expect(rows[1].querySelector('i.pi-minus')).not.toBeNull();
    expect(rows[1].textContent).toContain('—');
  });

  it('sends an unfiltered query body by default', () => {
    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush(roles);

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(EMPTY_QUERY);
    req.flush(users);
  });

  it('still loads the list when the role lookup fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne(queryUrl).flush(users);

    expect(api()['roles']()).toEqual([]);
    expect(api()['users']()).toEqual(users);
  });

  it('builds the role dropdown options from the lookup', () => {
    initAndFlush();

    expect(api()['roleOptions']).toEqual([
      { roleId: 'Admin', label: 'Administrator (Admin)' },
      { roleId: 'User', label: 'User (User)' },
    ]);
  });

  it('applies drawer filters, resets paging, and re-queries', () => {
    initAndFlush();
    api()['page'] = { first: 40, rows: 20 };

    api()['openFilterDrawer']();
    api()['draftFilters'] = {
      keyword: 'helen',
      isActive: true,
      roleId: 'Admin',
      passwordUpdatedFrom: new Date(2026, 0, 1),
      passwordUpdatedTo: new Date(2026, 0, 31),
    };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({
      keyword: 'helen',
      isActive: true,
      roleId: 'Admin',
      passwordUpdatedFrom: '2026-01-01',
      passwordUpdatedTo: '2026-01-31',
    });
    req.flush([users[0]]);

    expect(api()['page'].first).toBe(0);
    expect(api()['filterDrawerVisible']()).toBeFalse();
    expect(api()['users']().length).toBe(1);
  });

  it('counts the applied filters for the 搜尋條件 badge', () => {
    initAndFlush();
    expect(api()['activeFilterCount']).toBe(0);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')).toBeNull();

    api()['draftFilters'] = {
      keyword: 'helen',
      isActive: true,
      roleId: 'Admin',
      passwordUpdatedFrom: null,
      passwordUpdatedTo: null,
    };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([users[0]]);
    fixture.detectChanges();

    expect(api()['activeFilterCount']).toBe(3);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')?.textContent.trim()).toBe('3');
  });

  /** 停用 is a real filter; a falsy check would silently drop it from the badge. */
  it('counts a false 啟用 selection as an applied filter', () => {
    initAndFlush();

    api()['draftFilters'] = {
      keyword: null,
      isActive: false,
      roleId: null,
      passwordUpdatedFrom: null,
      passwordUpdatedTo: null,
    };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.isActive).toBeFalse();
    req.flush([users[1]]);

    expect(api()['activeFilterCount']).toBe(1);
    expect(api()['hasActiveFilters']).toBeTrue();
  });

  it('clears filters back to an unfiltered query', () => {
    initAndFlush();
    api()['draftFilters'] = {
      keyword: 'helen',
      isActive: false,
      roleId: null,
      passwordUpdatedFrom: null,
      passwordUpdatedTo: null,
    };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([users[0]]);

    api()['clearFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(EMPTY_QUERY);
    req.flush(users);
    expect(api()['hasActiveFilters']).toBeFalse();
  });

  it('persists applied filters, sort, and page to session storage', () => {
    initAndFlush();

    api()['draftFilters'] = {
      keyword: 'helen',
      isActive: null,
      roleId: null,
      passwordUpdatedFrom: null,
      passwordUpdatedTo: null,
    };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([users[0]]);

    api()['onSort']({ field: 'userName', order: -1 });
    api()['onPage']({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('app-user-list-filters')!).keyword).toBe('helen');
    expect(JSON.parse(sessionStorage.getItem('app-user-list-sort')!)).toEqual({
      field: 'userName',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('app-user-list-page')!)).toEqual({
      first: 20,
      rows: 50,
    });
  });

  it('restores persisted filters on init', () => {
    sessionStorage.setItem(
      'app-user-list-filters',
      JSON.stringify({ ...EMPTY_QUERY, keyword: 'retired', isActive: false }),
    );

    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush(roles);

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ ...EMPTY_QUERY, keyword: 'retired', isActive: false });
    req.flush([users[1]]);
  });

  it('honours an incoming roleId query param and leaves the rest of the state alone', async () => {
    await setup({ roleId: 'Admin' });
    sessionStorage.setItem(
      'app-user-list-filters',
      JSON.stringify({ ...EMPTY_QUERY, keyword: 'helen' }),
    );

    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush(roles);

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ ...EMPTY_QUERY, keyword: 'helen', roleId: 'Admin' });
    req.flush([users[0]]);
  });

  it('empties the list when the query fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(rolesUrl).flush(roles);
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['users']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates to the add, view, and edit routes', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['add']();
    expect(navigate).toHaveBeenCalledWith(['/app-users/new']);

    api()['view'](users[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-users', 'helen']);

    api()['edit'](users[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-users', 'helen', 'edit']);
  });

  it('warns about the role assignments that the delete will take with it', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    let message = '';
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      message = options.message;
      return confirmationService;
    });

    api()['confirmDelete'](users[0]);
    expect(message).toContain('主代碼 <b>1</b>');
    expect(message).toContain('此使用者仍有 2 個角色關聯，將一併刪除。');

    api()['confirmDelete'](users[1]);
    expect(message).not.toContain('角色關聯');
  });

  it('deletes the user and reloads once the confirmation is accepted', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](users[0]);

    const deleteReq = httpMock.expectOne(`${environment.apiUrl}/app-users/helen`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([users[1]]);
    expect(api()['users']().length).toBe(1);
  });
});
