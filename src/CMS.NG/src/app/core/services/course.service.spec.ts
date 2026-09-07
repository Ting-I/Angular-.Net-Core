import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { CourseService } from './course.service';
import { Course, CourseRequest } from '@core/models/course.model';

describe('CourseService', () => {
  let service: CourseService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/courses`;

  const azure: Course = {
    pkid: 1,
    title: 'Azure 系統管理',
    officialTitle: 'Microsoft Azure Administrator',
    courseId: 'AZ-104',
    prodCourseId: 'P-AZ-104',
    friendlyUrl: 'az-104',
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: 2,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 21,
    listPrice: 24000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partner: { pkid: 1, name: 'Microsoft', appKey: 'MS' },
    courseGroup: { pkid: 2, description: '雲端技術' },
    publishStatus: { pkid: 2, description: '已上架' },
    courseFaqCount: 4,
    courseRelatedLinkCount: 2,
    hotCourseCount: 1,
    courseRecommCount: 3,
    certificationPkids: [7, 9],
    jobCategoryPkids: [3],
  };

  const request: CourseRequest = {
    pkid: 0,
    title: '思科網路',
    officialTitle: null,
    courseId: 'CCNA',
    prodCourseId: 'P-CCNA',
    friendlyUrl: 'ccna',
    displayOrder: 2,
    partnerPkid: 2,
    courseGroupPkid: null,
    publishStatusPkid: 1,
    scheduleOn: '2026-03-01',
    scheduleOff: '2036-03-01',
    hour: 35,
    listPrice: 38000,
    learningCredit: 5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: false,
    certificationPkids: [],
    jobCategoryPkids: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CourseService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: Course[] | undefined;
    service.getAll().subscribe((courses) => (result = courses));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([azure]);

    expect(result).toEqual([azure]);
  });

  it('query() POSTs the filter body to /query', () => {
    let result: Course[] | undefined;
    service.query({ keyword: 'Azure', partnerPkid: 1 }).subscribe((c) => (result = c));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'Azure', partnerPkid: 1 });
    req.flush([azure]);

    expect(result?.length).toBe(1);
  });

  it('query() carries canRepeat=false rather than dropping it', () => {
    service.query({ keyword: null, canRepeat: false }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({ keyword: null, canRepeat: false });
    req.flush([]);
  });

  it('query() sends the date bounds as ISO strings', () => {
    service.query({ scheduleOnFrom: '2026-01-01', scheduleOffTo: '2030-12-31' }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({
      scheduleOnFrom: '2026-01-01',
      scheduleOffTo: '2030-12-31',
    });
    req.flush([]);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the numeric record route', () => {
    let result: Course | undefined;
    service.getById(1).subscribe((course) => (result = course));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(azure);

    expect(result?.certificationPkids).toEqual([7, 9]);
  });

  it('create() POSTs the request body to the collection route', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...azure, pkid: 2, courseId: 'CCNA' });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    service.update({ ...request, pkid: 1 }).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(azure);
  });

  it('delete() issues DELETE to the numeric record route', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('copy() POSTs the new courseId to the copy route', () => {
    let result: Course | undefined;
    service.copy(1, { newCourseId: 'AZ-104-COPY' }).subscribe((course) => (result = course));

    const req = httpMock.expectOne(`${baseUrl}/1/copy`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ newCourseId: 'AZ-104-COPY' });
    req.flush({ ...azure, pkid: 5, courseId: 'AZ-104-COPY' });

    expect(result?.pkid).toBe(5);
  });
});
