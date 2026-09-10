import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { AppUserService } from './app-user.service';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';

describe('AppUserService', () => {
  let service: AppUserService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/app-users`;

  const helen: AppUser = {
    pkid: 1,
    userId: 'helen',
    userName: 'Helen Lin',
    isActive: true,
    passwordUpdatedTime: '2026-01-15T12:00:00',
    roleCount: 2,
    roleIds: ['Admin', 'Editor'],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppUserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: AppUser[] | undefined;
    service.getAll().subscribe((users) => (result = users));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([helen]);

    expect(result).toEqual([helen]);
  });

  it('query() POSTs the filter body to /query', () => {
    let result: AppUser[] | undefined;
    service
      .query({ keyword: 'helen', isActive: true, roleId: 'Admin' })
      .subscribe((users) => (result = users));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'helen', isActive: true, roleId: 'Admin' });
    req.flush([helen]);

    expect(result?.length).toBe(1);
  });

  it('query() carries a false isActive rather than dropping it', () => {
    service.query({ isActive: false }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({ isActive: false });
    req.flush([]);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the record route', () => {
    let result: AppUser | undefined;
    service.getById('helen').subscribe((user) => (result = user));

    const req = httpMock.expectOne(`${baseUrl}/helen`);
    expect(req.request.method).toBe('GET');
    req.flush(helen);

    expect(result?.roleIds).toEqual(['Admin', 'Editor']);
  });

  it('getById() URL-encodes the string primary key', () => {
    service.getById('domain\\user name').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/domain%5Cuser%20name`);
    expect(req.request.method).toBe('GET');
    req.flush(helen);
  });

  it('create() POSTs the request body to the collection route', () => {
    const request: AppUserRequest = {
      userId: 'miles',
      userName: 'Miles Sun',
      isActive: true,
      roleIds: ['Editor'],
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...helen, userId: 'miles' });
  });

  it('create() never sends a password field', () => {
    service
      .create({ userId: 'miles', userName: 'Miles Sun', isActive: true, roleIds: [] })
      .subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(Object.keys(req.request.body).sort()).toEqual([
      'isActive',
      'roleIds',
      'userId',
      'userName',
    ]);
    req.flush(helen);
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const request: AppUserRequest = {
      userId: 'helen',
      userName: '林海倫',
      isActive: false,
      roleIds: [],
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.userId).toBe('helen');
    expect(req.request.body.passwordHash).toBeUndefined();
    req.flush(helen);
  });

  it('resetPassword() POSTs an empty body to the encoded sub-route', () => {
    service.resetPassword('domain\\user name').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/domain%5Cuser%20name/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
  });

  it('delete() issues DELETE to the encoded record route', () => {
    service.delete('domain\\user name').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/domain%5Cuser%20name`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
