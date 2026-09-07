import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { PublishStatusService } from './publish-status.service';
import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';

describe('PublishStatusService', () => {
  let service: PublishStatusService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/publish-statuses`;

  const published: PublishStatus = {
    pkid: 2,
    description: '已發布',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false,
    courseCount: 12,
    promotionCount: 4,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PublishStatusService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: PublishStatus[] | undefined;
    service.getAll().subscribe((statuses) => (result = statuses));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([published]);

    expect(result).toEqual([published]);
  });

  it('query() POSTs the filter body to /query', () => {
    let result: PublishStatus[] | undefined;
    service
      .query({ keyword: '發布', isPublished: true })
      .subscribe((statuses) => (result = statuses));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: '發布', isPublished: true });
    req.flush([published]);

    expect(result?.length).toBe(1);
  });

  it('query() carries a false bool filter rather than dropping it', () => {
    service.query({ keyword: null, isDraft: false }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({ keyword: null, isDraft: false });
    req.flush([]);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the numeric record route', () => {
    let result: PublishStatus | undefined;
    service.getById(2).subscribe((status) => (result = status));

    const req = httpMock.expectOne(`${baseUrl}/2`);
    expect(req.request.method).toBe('GET');
    req.flush(published);

    expect(result?.courseCount).toBe(12);
  });

  it('create() POSTs the request body, key included, to the collection route', () => {
    const request: PublishStatusRequest = {
      pkid: 4,
      description: '審核中',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    };

    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...published, pkid: 4 });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    const request: PublishStatusRequest = {
      pkid: 2,
      description: '已上架',
      isDraft: false,
      isPublished: true,
      isDiscontinued: false,
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(2);
    req.flush(published);
  });

  it('delete() issues DELETE to the numeric record route', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
