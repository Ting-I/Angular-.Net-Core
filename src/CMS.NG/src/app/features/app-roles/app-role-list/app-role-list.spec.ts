import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { AppRoleList } from './app-role-list';
import { AppRole } from '@core/models/app-role.model';

describe('AppRoleList', () => {
  let fixture: ComponentFixture<AppRoleList>;
  let component: AppRoleList;
  let httpMock: HttpTestingController;
  const queryUrl = `${environment.apiUrl}/app-roles/query`;

  const roles: AppRole[] = [
    {
      pkid: 1,
      roleId: 'Admin',
      roleName: 'Administrator',
      permissionLevel: 1,
      description: '系統管理員',
      userCount: 3,
      userIds: [],
    },
    {
      pkid: 2,
      roleId: 'User',
      roleName: 'User',
      permissionLevel: 100,
      description: '一般使用者',
      userCount: 9,
      userIds: [],
    },
  ];

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function initAndFlush(payload: AppRole[] = roles): void {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  it('creates and loads the role list on init', () => {
    initAndFlush();

    expect(component).toBeTruthy();
    expect(api()['roles']()).toEqual(roles);
    expect(api()['loading']()).toBeFalse();
  });

  it('renders one row per role with its user count', () => {
    initAndFlush();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Admin');
    expect(rows[0].textContent).toContain('系統管理員');
    expect(rows[0].textContent).toContain('3');
  });

  it('sends an unfiltered query body by default', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: null, permissionLevel: null });
    req.flush(roles);
  });

  it('applies drawer filters, resets paging, and re-queries', () => {
    initAndFlush();
    api()['page'] = { first: 40, rows: 20 };

    api()['openFilterDrawer']();
    api()['draftFilters'] = { keyword: 'admin', permissionLevel: 1 };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: 'admin', permissionLevel: 1 });
    req.flush([roles[0]]);

    expect(api()['page'].first).toBe(0);
    expect(api()['filterDrawerVisible']()).toBeFalse();
    expect(api()['roles']().length).toBe(1);
  });

  it('counts the applied filters for the 搜尋條件 badge', () => {
    initAndFlush();
    expect(api()['activeFilterCount']).toBe(0);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')).toBeNull();

    api()['draftFilters'] = { keyword: 'admin', permissionLevel: 1 };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([roles[0]]);
    fixture.detectChanges();

    expect(api()['activeFilterCount']).toBe(2);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')?.textContent.trim()).toBe('2');
  });

  it('reports active filters only when a filter is set', () => {
    initAndFlush();
    expect(api()['hasActiveFilters']).toBeFalse();

    api()['draftFilters'] = { keyword: 'admin', permissionLevel: null };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([roles[0]]);

    expect(api()['hasActiveFilters']).toBeTrue();
  });

  it('clears filters back to an unfiltered query', () => {
    initAndFlush();
    api()['draftFilters'] = { keyword: 'admin', permissionLevel: 1 };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([roles[0]]);

    api()['clearFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: null, permissionLevel: null });
    req.flush(roles);
    expect(api()['hasActiveFilters']).toBeFalse();
  });

  it('persists applied filters, sort, and page to session storage', () => {
    initAndFlush();

    api()['draftFilters'] = { keyword: 'admin', permissionLevel: null };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([roles[0]]);

    api()['onSort']({ field: 'permissionLevel', order: -1 });
    api()['onPage']({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('app-role-list-filters')!).keyword).toBe('admin');
    expect(JSON.parse(sessionStorage.getItem('app-role-list-sort')!)).toEqual({
      field: 'permissionLevel',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('app-role-list-page')!)).toEqual({
      first: 20,
      rows: 50,
    });
  });

  it('restores persisted filters on init', () => {
    sessionStorage.setItem(
      'app-role-list-filters',
      JSON.stringify({ keyword: 'user', permissionLevel: 100 }),
    );

    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: 'user', permissionLevel: 100 });
    req.flush([roles[1]]);
  });

  it('empties the list when the query fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['roles']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates to the add, view, and edit routes', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['add']();
    expect(navigate).toHaveBeenCalledWith(['/app-roles/new']);

    api()['view'](roles[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin']);

    api()['edit'](roles[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 'Admin', 'edit']);
  });

  it('deletes the role and reloads once the confirmation is accepted', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](roles[0]);

    const deleteReq = httpMock.expectOne(`${environment.apiUrl}/app-roles/Admin`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([roles[1]]);
    expect(api()['roles']().length).toBe(1);
  });
});
