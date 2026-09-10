import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { CourseGroupService } from './course-group.service';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';

describe('CourseGroupService', () => {
  let service: CourseGroupService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/course-groups`;

  const cloud: CourseGroup = {
    pkid: 1,
    description: '雲端技術',
    courseCount: 12,
    partnerCourseGroupCount: 3,
  };

  const request: CourseGroupRequest = {
    pkid: 0,
    description: '網路安全',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CourseGroupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: CourseGroup[] | undefined;
    service.getAll().subscribe((courseGroups) => (result = courseGroups));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([cloud]);

    expect(result).toEqual([cloud]);
  });

  it('query() POSTs the filter body to /query', () => {
    let result: CourseGroup[] | undefined;
    service.query({ keyword: '雲端', inUse: true }).subscribe((cg) => (result = cg));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: '雲端', inUse: true });
    req.flush([cloud]);

    expect(result?.length).toBe(1);
  });

  it('query() carries inUse=false rather than dropping it', () => {
    service.query({ keyword: null, inUse: false }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({ keyword: null, inUse: false });
    req.flush([]);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the numeric record route', () => {
    let result: CourseGroup | undefined;
    service.getById(1).subscribe((courseGroup) => (result = courseGroup));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(cloud);

    expect(result?.courseCount).toBe(12);
  });

  it('create() POSTs the request body to the collection route', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...cloud, pkid: 2, description: '網路安全' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    service.update({ ...request, pkid: 1, description: '雲端運算' }).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(cloud);
  });

  it('delete() issues DELETE to the numeric record route', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
