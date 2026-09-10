import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { PartnerService } from './partner.service';
import { Partner, PartnerRequest } from '@core/models/partner.model';

describe('PartnerService', () => {
  let service: PartnerService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/partners`;

  const microsoft: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: '微軟 Microsoft',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 1,
    imageFilename: 'ms.png',
    certificationCount: 4,
    courseCount: 12,
    courseGroupCount: 3,
    promotionCount: 2,
    seminarCount: 1,
  };

  const request: PartnerRequest = {
    pkid: 0,
    name: 'Cisco',
    appKey: 'CSCO',
    nameOnPartnerMenu: '思科 Cisco',
    nameOnCourseDetailPage: '思科',
    displayOrder: 2,
    imageFilename: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PartnerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: Partner[] | undefined;
    service.getAll().subscribe((partners) => (result = partners));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([microsoft]);

    expect(result).toEqual([microsoft]);
  });

  it('query() POSTs the filter body to /query', () => {
    let result: Partner[] | undefined;
    service.query({ keyword: 'Microsoft', hasImage: true }).subscribe((p) => (result = p));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'Microsoft', hasImage: true });
    req.flush([microsoft]);

    expect(result?.length).toBe(1);
  });

  it('query() carries hasImage=false rather than dropping it', () => {
    service.query({ keyword: null, hasImage: false }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({ keyword: null, hasImage: false });
    req.flush([]);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the numeric record route', () => {
    let result: Partner | undefined;
    service.getById(1).subscribe((partner) => (result = partner));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(microsoft);

    expect(result?.courseCount).toBe(12);
  });

  it('create() POSTs the request body to the collection route', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...microsoft, pkid: 2, name: 'Cisco' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    service.update({ ...request, pkid: 1, name: 'Microsoft' }).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(microsoft);
  });

  it('delete() issues DELETE to the numeric record route', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
