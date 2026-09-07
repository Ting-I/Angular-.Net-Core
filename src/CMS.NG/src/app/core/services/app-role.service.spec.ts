import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { AppRoleService } from './app-role.service';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';

describe('AppRoleService', () => {
  let service: AppRoleService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/app-roles`;

  const adminRole: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3,
    userIds: ['helen', 'miles'],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppRoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: AppRole[] | undefined;
    service.getAll().subscribe((roles) => (result = roles));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([adminRole]);

    expect(result).toEqual([adminRole]);
  });

  it('query() POSTs the filter body to /query', () => {
    let result: AppRole[] | undefined;
    service.query({ keyword: 'admin', permissionLevel: 1 }).subscribe((roles) => (result = roles));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'admin', permissionLevel: 1 });
    req.flush([adminRole]);

    expect(result?.length).toBe(1);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the record route', () => {
    let result: AppRole | undefined;
    service.getById('Admin').subscribe((role) => (result = role));

    const req = httpMock.expectOne(`${baseUrl}/Admin`);
    expect(req.request.method).toBe('GET');
    req.flush(adminRole);

    expect(result?.userIds).toEqual(['helen', 'miles']);
  });

  it('getById() URL-encodes the string primary key', () => {
    service.getById('Power User/Ops').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/Power%20User%2FOps`);
    expect(req.request.method).toBe('GET');
    req.flush(adminRole);
  });

  it('create() POSTs the request body to the collection route', () => {
    const request: AppRoleRequest = {
      roleId: 'Editor',
      roleName: 'Editor',
      permissionLevel: 50,
      description: '內容編輯',
      userIds: ['helen'],
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...adminRole, roleId: 'Editor' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const request: AppRoleRequest = {
      roleId: 'Admin',
      roleName: '系統管理者',
      permissionLevel: 2,
      description: null,
      userIds: [],
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.roleId).toBe('Admin');
    req.flush(adminRole);
  });

  it('delete() issues DELETE to the encoded record route', () => {
    service.delete('Power User').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/Power%20User`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
