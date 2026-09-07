import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { CourseList } from './course-list';
import { Course } from '@core/models/course.model';

describe('CourseList', () => {
  let fixture: ComponentFixture<CourseList>;
  let component: CourseList;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/courses`;
  const queryUrl = `${baseUrl}/query`;
  const lookupUrl = `${environment.apiUrl}/lookups`;

  function makeCourse(overrides: Partial<Course> = {}): Course {
    return {
      pkid: 1,
      title: 'Azure 系統管理',
      officialTitle: null,
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
      courseFaqCount: 0,
      courseRelatedLinkCount: 0,
      hotCourseCount: 0,
      courseRecommCount: 0,
      certificationPkids: [],
      jobCategoryPkids: [],
      ...overrides,
    };
  }

  const courses: Course[] = [
    makeCourse(),
    makeCourse({
      pkid: 2,
      title: '思科網路',
      courseId: 'CCNA',
      prodCourseId: 'P-CCNA',
      displayOrder: 2,
      partnerPkid: 2,
      courseGroupPkid: null,
      canRepeat: false,
      partner: { pkid: 2, name: 'Cisco', appKey: 'CSCO' },
      courseGroup: null,
      courseFaqCount: 4,
      courseRelatedLinkCount: 3,
      hotCourseCount: 2,
      courseRecommCount: 1,
    }),
  ];

  const unfiltered = {
    keyword: null,
    partnerPkid: null,
    courseGroupPkid: null,
    publishStatusPkid: null,
    scheduleOnFrom: null,
    scheduleOnTo: null,
    scheduleOffFrom: null,
    scheduleOffTo: null,
    canRepeat: null,
  };

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  async function setup(queryParams: Record<string, string> = {}): Promise<void> {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [CourseList],
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

    fixture = TestBed.createComponent(CourseList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Satisfies the three drawer lookups the list forkJoins before its first query. */
  function flushLookups(): void {
    httpMock.expectOne(`${lookupUrl}/partners`).flush([
      { pkid: 1, name: 'Microsoft', appKey: 'MS' },
      { pkid: 2, name: 'Cisco', appKey: 'CSCO' },
    ]);
    httpMock.expectOne(`${lookupUrl}/course-groups`).flush([{ pkid: 2, description: '雲端技術' }]);
    httpMock.expectOne(`${lookupUrl}/publish-statuses`).flush([{ pkid: 2, description: '已上架' }]);
  }

  function initAndFlush(payload: Course[] = courses): void {
    fixture.detectChanges();
    flushLookups();
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await setup();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('creates and loads the course list on init', () => {
    initAndFlush();

    expect(component).toBeTruthy();
    expect(api()['courses']()).toEqual(courses);
    expect(api()['loading']()).toBeFalse();
  });

  it('renders one row per course', () => {
    initAndFlush();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Azure 系統管理');
    expect(rows[1].textContent).toContain('思科網路');
  });

  /** The FK labels come from the JOINed nav objects, not a client-side lookup map. */
  it('renders the three nav-object labels from the payload', () => {
    initAndFlush();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr')[0].querySelectorAll('td');
    expect(cells[5].textContent.trim()).toBe('Microsoft');
    expect(cells[6].textContent.trim()).toBe('雲端技術');
    expect(cells[7].textContent.trim()).toBe('已上架');
  });

  it('renders an em dash when the nullable course group is null', () => {
    initAndFlush();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr')[1].querySelectorAll('td');
    expect(cells[6].textContent.trim()).toBe('—');
  });

  it('formats the two decimal columns at their own scales', () => {
    initAndFlush();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr')[0].querySelectorAll('td');
    expect(cells[11].textContent.trim()).toBe('24,000');
    expect(cells[12].textContent.trim()).toBe('3.5');
  });

  it('sends an unfiltered query body by default', () => {
    fixture.detectChanges();
    flushLookups();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(courses);
  });

  it('defaults the sort to displayOrder ascending', () => {
    initAndFlush();

    expect(api()['sort']).toEqual({ field: 'displayOrder', order: 1 });
  });

  it('applies drawer filters, resets paging, and re-queries', () => {
    initAndFlush();
    api()['page'] = { first: 40, rows: 20 };

    api()['openFilterDrawer']();
    api()['draftFilters'] = {
      ...api()['draftFilters'],
      keyword: 'Azure',
      partnerPkid: 1,
    };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ ...unfiltered, keyword: 'Azure', partnerPkid: 1 });
    req.flush([courses[0]]);

    expect(api()['page'].first).toBe(0);
    expect(api()['filterDrawerVisible']()).toBeFalse();
    expect(api()['courses']().length).toBe(1);
  });

  it('serialises the date filters with local components', () => {
    initAndFlush();

    api()['draftFilters'] = {
      ...api()['draftFilters'],
      // Late evening: toISOString() would report 2025-12-31 for a UTC+8 client.
      scheduleOnFrom: new Date(2026, 0, 1, 23, 30),
    };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.scheduleOnFrom).toBe('2026-01-01');
    req.flush(courses);
  });

  it('treats canRepeat=false as an active filter', () => {
    initAndFlush();
    expect(api()['hasActiveFilters']).toBeFalse();

    api()['draftFilters'] = { ...api()['draftFilters'], canRepeat: false };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.canRepeat).toBeFalse();
    req.flush([courses[1]]);

    expect(api()['hasActiveFilters']).toBeTrue();
    expect(api()['activeFilterCount']).toBe(1);
  });

  it('counts every applied filter for the 搜尋條件 badge', () => {
    initAndFlush();
    expect(api()['activeFilterCount']).toBe(0);

    api()['draftFilters'] = {
      keyword: 'Azure',
      partnerPkid: 1,
      courseGroupPkid: 2,
      publishStatusPkid: 2,
      scheduleOnFrom: new Date(2026, 0, 1),
      scheduleOnTo: new Date(2026, 11, 31),
      scheduleOffFrom: new Date(2027, 0, 1),
      scheduleOffTo: new Date(2027, 11, 31),
      canRepeat: false,
    };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([courses[0]]);
    fixture.detectChanges();

    expect(api()['activeFilterCount']).toBe(9);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')?.textContent.trim()).toBe('9');
  });

  it('shows no badge when nothing is filtered', () => {
    initAndFlush();

    expect(fixture.nativeElement.querySelector('p-button .p-badge')).toBeNull();
  });

  it('clears filters back to an unfiltered query', () => {
    initAndFlush();
    api()['draftFilters'] = { ...api()['draftFilters'], keyword: 'Azure' };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([]);

    api()['clearFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(courses);
    expect(api()['hasActiveFilters']).toBeFalse();
  });

  it('persists applied filters, sort, and page to session storage', () => {
    initAndFlush();

    api()['draftFilters'] = { ...api()['draftFilters'], keyword: 'Azure' };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([courses[0]]);

    api()['onSort']({ field: 'title', order: -1 });
    api()['onPage']({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('course-list-filters')!).keyword).toBe('Azure');
    expect(JSON.parse(sessionStorage.getItem('course-list-sort')!)).toEqual({
      field: 'title',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('course-list-page')!)).toEqual({
      first: 20,
      rows: 50,
    });
  });

  it('restores persisted filters on init', async () => {
    sessionStorage.setItem(
      'course-list-filters',
      JSON.stringify({ ...unfiltered, partnerPkid: 2 }),
    );

    fixture.detectChanges();
    flushLookups();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ ...unfiltered, partnerPkid: 2 });
    req.flush([courses[1]]);
  });

  it('empties the list when the query fails', () => {
    fixture.detectChanges();
    flushLookups();
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['courses']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  it('still loads the list when the lookups fail', () => {
    fixture.detectChanges();
    httpMock
      .expectOne(`${lookupUrl}/partners`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    httpMock.expectOne(`${lookupUrl}/course-groups`).flush([]);
    httpMock.expectOne(`${lookupUrl}/publish-statuses`).flush([]);

    httpMock.expectOne(queryUrl).flush(courses);
    expect(api()['courses']().length).toBe(2);
  });

  it('navigates to the add, view, and edit routes', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['add']();
    expect(navigate).toHaveBeenCalledWith(['/courses/new']);

    api()['view'](courses[0]);
    expect(navigate).toHaveBeenCalledWith(['/courses', 1]);

    api()['edit'](courses[0]);
    expect(navigate).toHaveBeenCalledWith(['/courses', 1, 'edit']);
  });

  // ---------- Delete ----------

  it('warns in the confirmation message when the course is still referenced', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](courses[1]);

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('<b>2</b>');
    expect(message).toContain('CCNA 思科網路');
    expect(message).toContain('4 筆課程問答');
    expect(message).toContain('1 筆推薦課程');
  });

  it('omits the warning when nothing references the course', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](courses[0]);

    expect(confirm.calls.mostRecent().args[0].message).not.toContain('無法刪除');
  });

  it('escapes record text before it reaches the HTML confirm message', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](makeCourse({ title: '<img src=x>' }));

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('&lt;img src=x&gt;');
    expect(message).not.toContain('<img');
  });

  it('deletes the course and reloads once the confirmation is accepted', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](courses[0]);

    const deleteReq = httpMock.expectOne(`${baseUrl}/1`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([courses[1]]);
    expect(api()['courses']().length).toBe(1);
  });

  it('keeps the row when the API rejects the delete with 409', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](courses[1]);

    httpMock
      .expectOne(`${baseUrl}/2`)
      .flush({ title: '課程仍被使用' }, { status: 409, statusText: 'Conflict' });

    // No reload is issued on failure.
    httpMock.expectNone(queryUrl);
    expect(api()['courses']().length).toBe(2);
  });

  // ---------- Copy ----------

  it('opens the copy dialog with the source course and an empty code', () => {
    initAndFlush();

    api()['openCopyDialog'](courses[0]);

    expect(api()['copyDialogVisible']()).toBeTrue();
    expect(api()['copySource']()).toEqual(courses[0]);
    expect(api()['copyCourseId']).toBe('');
    expect(api()['copyError']()).toBeNull();
  });

  it('rejects an empty code without calling the API', () => {
    initAndFlush();
    api()['openCopyDialog'](courses[0]);

    api()['copyCourseId'] = '   ';
    api()['confirmCopy']();

    httpMock.expectNone(`${baseUrl}/1/copy`);
    expect(api()['copyError']()).toBe('簡介代碼為必填。');
  });

  it('copies the course and navigates to the new record', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['openCopyDialog'](courses[0]);
    api()['copyCourseId'] = 'AZ-104-COPY';
    api()['confirmCopy']();

    const req = httpMock.expectOne(`${baseUrl}/1/copy`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ newCourseId: 'AZ-104-COPY' });
    req.flush(makeCourse({ pkid: 5, courseId: 'AZ-104-COPY' }));

    expect(navigate).toHaveBeenCalledWith(['/courses', 5]);
    expect(api()['copyDialogVisible']()).toBeFalse();
  });

  it('keeps the copy dialog open and explains a 409', () => {
    initAndFlush();

    api()['openCopyDialog'](courses[0]);
    api()['copyCourseId'] = 'CCNA';
    api()['confirmCopy']();

    httpMock
      .expectOne(`${baseUrl}/1/copy`)
      .flush({ title: '簡介代碼已存在' }, { status: 409, statusText: 'Conflict' });

    expect(api()['copyDialogVisible']()).toBeTrue();
    expect(api()['copyError']()).toBe('簡介代碼已存在。');
    expect(api()['copying']()).toBeFalse();
  });

  // ---------- Cross-entity navigation ----------

  describe('with an incoming partnerPkid query param', () => {
    beforeEach(async () => {
      TestBed.resetTestingModule();
      await setup({ partnerPkid: '2' });
    });

    it('overrides only that saved filter and re-queries', () => {
      sessionStorage.setItem(
        'course-list-filters',
        JSON.stringify({ ...unfiltered, keyword: 'Azure', partnerPkid: 1 }),
      );

      fixture.detectChanges();
      flushLookups();

      const req = httpMock.expectOne(queryUrl);
      // The incoming param wins for partnerPkid; the saved keyword survives.
      expect(req.request.body.partnerPkid).toBe(2);
      expect(req.request.body.keyword).toBe('Azure');
      req.flush([courses[1]]);
    });
  });
});
