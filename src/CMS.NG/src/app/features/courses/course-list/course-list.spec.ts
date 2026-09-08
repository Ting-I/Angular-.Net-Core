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

  // ---------- Inline cell editing ----------

  describe('inline cell editing', () => {
    /** Column order in the body row; the three read-only ones are noted alongside. */
    const CELL = {
      pkid: 0, // read-only
      displayOrder: 1,
      courseId: 2,
      prodCourseId: 3,
      title: 4,
      partner: 5, // read-only
      courseGroup: 6, // read-only
      publishStatus: 7,
      scheduleOn: 8,
      scheduleOff: 9,
      hour: 10,
      listPrice: 11,
      learningCredit: 12,
      canRepeat: 13,
    };

    function cellAt(row: number, index: number): HTMLTableCellElement {
      return fixture.nativeElement
        .querySelectorAll('tbody tr')
        [row].querySelectorAll('td')[index] as HTMLTableCellElement;
    }

    function doubleClick(cell: HTMLElement): void {
      cell.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      fixture.detectChanges();
    }

    /** Enters edit mode through the component so a test can go straight to the draft value. */
    function openEditor(course: Course, field: string): void {
      api()['startEdit'](course, field);
      fixture.detectChanges();
    }

    /** The full record the save re-reads before it writes — carries both n-n key arrays. */
    const fullCourse = makeCourse({ certificationPkids: [7, 8], jobCategoryPkids: [9] });

    // --- entering edit mode ---

    it('enters edit mode on a double-click and renders an editor in the cell', () => {
      initAndFlush();

      const cell = cellAt(0, CELL.title);
      doubleClick(cell);

      expect(api()['editingCell']()).toEqual({ pkid: 1, field: 'title' });
      expect(cell.querySelector('input')).not.toBeNull();
    });

    it('does not enter edit mode on a single click', () => {
      initAndFlush();

      const cell = cellAt(0, CELL.title);
      cell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      fixture.detectChanges();

      expect(api()['editingCell']()).toBeNull();
      expect(cell.querySelector('input')).toBeNull();
      expect(cell.textContent).toContain('Azure 系統管理');
    });

    it('seeds the draft from the row it opens', () => {
      initAndFlush();

      openEditor(courses[0], 'title');
      expect(api()['draft'].text).toBe('Azure 系統管理');

      api()['cancelEdit']();
      openEditor(courses[0], 'hour');
      expect(api()['draft'].number).toBe(21);

      api()['cancelEdit']();
      openEditor(courses[0], 'scheduleOn');
      expect(api()['draft'].date).toEqual(new Date(2026, 0, 1));

      api()['cancelEdit']();
      openEditor(courses[0], 'publishStatusPkid');
      expect(api()['draft'].select).toBe(2);

      api()['cancelEdit']();
      openEditor(courses[0], 'canRepeat');
      expect(api()['draft'].checkbox).toBeTrue();
    });

    it('closes the editor on escape without calling the API', () => {
      initAndFlush();
      openEditor(courses[0], 'title');

      api()['draft'].text = '改到一半';
      api()['cancelEdit']();
      fixture.detectChanges();

      expect(api()['editingCell']()).toBeNull();
      httpMock.expectNone(baseUrl);
      expect(api()['courses']()[0].title).toBe('Azure 系統管理');
    });

    // --- read-only columns ---

    it('leaves the three read-only columns without an inline editor', () => {
      initAndFlush();

      for (const index of [CELL.pkid, CELL.partner, CELL.courseGroup]) {
        const cell = cellAt(0, index);
        expect(cell.classList).not.toContain('editable-cell');

        doubleClick(cell);

        expect(api()['editingCell']()).toBeNull();
        expect(cell.querySelector('input')).toBeNull();
      }
    });

    it('refuses to open a field that is not an editable column', () => {
      initAndFlush();

      // The key and the two FK columns behind 原廠 / 課程群組 are rejected by the guard, not only
      // by the template omitting the (dblclick).
      for (const field of ['pkid', 'partnerPkid', 'courseGroupPkid', 'friendlyUrl']) {
        api()['startEdit'](courses[0], field);
        expect(api()['editingCell']()).toBeNull();
      }
    });

    // --- persisting on blur ---

    it('persists the edited cell when the editor loses focus', () => {
      initAndFlush();

      const cell = cellAt(0, CELL.title);
      doubleClick(cell);

      const input = cell.querySelector('input') as HTMLInputElement;
      input.value = 'Azure 進階管理';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      input.dispatchEvent(new Event('blur'));
      fixture.detectChanges();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);

      const put = httpMock.expectOne(baseUrl);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body.pkid).toBe(1);
      expect(put.request.body.title).toBe('Azure 進階管理');
      put.flush(makeCourse({ title: 'Azure 進階管理' }));

      expect(api()['courses']()[0].title).toBe('Azure 進階管理');
      expect(api()['editingCell']()).toBeNull();
    });

    /**
     * The list payload never carries the n-n keys, and the update endpoint rewrites both junction
     * tables from the request. Saving the list row as-is would clear them.
     */
    it('re-reads the record so the update keeps the n-n keys', () => {
      initAndFlush();
      openEditor(courses[0], 'hour');

      api()['draft'].number = 28;
      api()['commit']();

      const get = httpMock.expectOne(`${baseUrl}/1`);
      expect(get.request.method).toBe('GET');
      get.flush(fullCourse);

      const put = httpMock.expectOne(baseUrl);
      expect(put.request.body.hour).toBe(28);
      expect(put.request.body.certificationPkids).toEqual([7, 8]);
      expect(put.request.body.jobCategoryPkids).toEqual([9]);
      put.flush(makeCourse({ hour: 28 }));
    });

    it('serialises an edited date with local components', () => {
      initAndFlush();
      openEditor(courses[0], 'scheduleOff');

      // Late evening: toISOString() would report 2036-12-30 for a UTC+8 client.
      api()['draft'].date = new Date(2036, 11, 31, 23, 30);
      api()['commit']();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      const put = httpMock.expectOne(baseUrl);
      expect(put.request.body.scheduleOff).toBe('2036-12-31');
      put.flush(makeCourse({ scheduleOff: '2036-12-31' }));
    });

    it('writes the selected 上架狀態 key and takes the new label from the response', () => {
      initAndFlush();
      openEditor(courses[0], 'publishStatusPkid');

      api()['draft'].select = 1;
      api()['commit']();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      const put = httpMock.expectOne(baseUrl);
      expect(put.request.body.publishStatusPkid).toBe(1);
      put.flush(
        makeCourse({ publishStatusPkid: 1, publishStatus: { pkid: 1, description: '未上架' } }),
      );

      expect(api()['courses']()[0].publishStatus?.description).toBe('未上架');
    });

    it('persists 允許重聽 as a boolean', () => {
      initAndFlush();
      openEditor(courses[0], 'canRepeat');

      api()['draft'].checkbox = false;
      api()['commit']();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      const put = httpMock.expectOne(baseUrl);
      expect(put.request.body.canRepeat).toBeFalse();
      put.flush(makeCourse({ canRepeat: false }));

      expect(api()['courses']()[0].canRepeat).toBeFalse();
    });

    it('closes without calling the API when the value did not change', () => {
      initAndFlush();
      openEditor(courses[0], 'title');

      api()['commit']();

      httpMock.expectNone(`${baseUrl}/1`);
      expect(api()['editingCell']()).toBeNull();
    });

    it('defers the commit while a picker overlay holds the focus', () => {
      initAndFlush();
      openEditor(courses[0], 'scheduleOn');

      // p-datepicker moves focus into its panel on open, which blurs the input.
      api()['editorOverlayOpen'].set(true);
      api()['draft'].date = new Date(2026, 5, 1);
      api()['commit']();

      httpMock.expectNone(`${baseUrl}/1`);
      expect(api()['editingCell']()).toEqual({ pkid: 1, field: 'scheduleOn' });

      api()['editorOverlayOpen'].set(false);
      api()['commit']();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      httpMock.expectOne(baseUrl).flush(makeCourse({ scheduleOn: '2026-06-01' }));
    });

    // --- validation ---

    /** Every case leaves the cell open with an inline message and writes nothing. */
    function expectRejected(field: string, seed: () => void, message: string): void {
      openEditor(courses[0], field);
      seed();
      api()['commit']();
      fixture.detectChanges();

      httpMock.expectNone(`${baseUrl}/1`);
      httpMock.expectNone(baseUrl);
      expect(api()['editError']()).toBe(message);
      expect(api()['editingCell']()).toEqual({ pkid: 1, field });

      api()['cancelEdit']();
      fixture.detectChanges();
    }

    it('rejects a cleared required text field', () => {
      initAndFlush();

      expectRejected('title', () => (api()['draft'].text = '   '), '課程名稱為必填。');
      expectRejected('courseId', () => (api()['draft'].text = ''), '簡介代碼為必填。');
      expectRejected('prodCourseId', () => (api()['draft'].text = ''), '科目代碼為必填。');
    });

    it('rejects text longer than the column', () => {
      initAndFlush();

      expectRejected(
        'courseId',
        () => (api()['draft'].text = 'A'.repeat(51)),
        '簡介代碼不可超過 50 個字元。',
      );
    });

    it('rejects a cleared numeric field', () => {
      initAndFlush();

      expectRejected('hour', () => (api()['draft'].number = null), '時數為必填，且必須為數字。');
      expectRejected(
        'listPrice',
        () => (api()['draft'].number = Number.NaN),
        '定價為必填，且必須為數字。',
      );
    });

    it('rejects a negative number in the three numeric columns', () => {
      initAndFlush();

      expectRejected('hour', () => (api()['draft'].number = -1), '時數不可小於 0。');
      expectRejected('listPrice', () => (api()['draft'].number = -0.5), '定價不可小於 0。');
      expectRejected('learningCredit', () => (api()['draft'].number = -3), '點數不可小於 0。');
    });

    it('rejects a fractional value in a whole-number column', () => {
      initAndFlush();

      expectRejected('hour', () => (api()['draft'].number = 7.5), '時數必須為整數。');
      expectRejected('listPrice', () => (api()['draft'].number = 100.25), '定價必須為整數。');
    });

    it('accepts one decimal place on 點數 but not two', () => {
      initAndFlush();

      expectRejected(
        'learningCredit',
        () => (api()['draft'].number = 3.25),
        '點數最多只能有 1 位小數。',
      );

      openEditor(courses[0], 'learningCredit');
      api()['draft'].number = 4.5;
      api()['commit']();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      httpMock.expectOne(baseUrl).flush(makeCourse({ learningCredit: 4.5 }));
      expect(api()['editError']()).toBeNull();
    });

    it('rejects a cleared or unparsable date', () => {
      initAndFlush();

      expectRejected(
        'scheduleOn',
        () => (api()['draft'].date = null),
        '上架日期為必填，且必須是有效日期。',
      );
      expectRejected(
        'scheduleOff',
        () => (api()['draft'].date = new Date('nope')),
        '下架日期為必填，且必須是有效日期。',
      );
    });

    it('rejects 上架日期 after 下架日期 from either end of the range', () => {
      initAndFlush();

      // Row 1 stores 2026-01-01 through 2036-01-01.
      expectRejected(
        'scheduleOn',
        () => (api()['draft'].date = new Date(2037, 0, 1)),
        '上架日期不可晚於下架日期。',
      );
      expectRejected(
        'scheduleOff',
        () => (api()['draft'].date = new Date(2025, 11, 31)),
        '上架日期不可晚於下架日期。',
      );
    });

    it('accepts 上架日期 equal to 下架日期', () => {
      initAndFlush();
      openEditor(courses[0], 'scheduleOff');

      api()['draft'].date = new Date(2026, 0, 1);
      api()['commit']();

      expect(api()['editError']()).toBeNull();
      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      httpMock.expectOne(baseUrl).flush(makeCourse({ scheduleOff: '2026-01-01' }));
    });

    it('rejects a cleared 上架狀態', () => {
      initAndFlush();

      expectRejected('publishStatusPkid', () => (api()['draft'].select = null), '上架狀態為必填。');
    });

    it('shows the inline message in the open cell and blocks a move to another cell', () => {
      initAndFlush();

      const cell = cellAt(0, CELL.title);
      doubleClick(cell);
      api()['draft'].text = '';
      api()['commit']();
      fixture.detectChanges();

      expect(cell.querySelector('.cell-error')?.textContent).toContain('課程名稱為必填。');

      // The invalid cell keeps the focus until it is fixed or cancelled.
      doubleClick(cellAt(0, CELL.hour));
      expect(api()['editingCell']()).toEqual({ pkid: 1, field: 'title' });

      api()['cancelEdit']();
      fixture.detectChanges();
    });

    // --- failed save ---

    it('reverts the cell and reports the error when the save is rejected', () => {
      initAndFlush();
      openEditor(courses[0], 'title');

      api()['draft'].text = 'Azure 進階管理';
      api()['commit']();

      httpMock.expectOne(`${baseUrl}/1`).flush(fullCourse);
      httpMock.expectOne(baseUrl).flush('boom', { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(api()['courses']()[0].title).toBe('Azure 系統管理');
      expect(api()['editingCell']()).toBeNull();
      expect(api()['savingCell']()).toBeFalse();
      expect(cellAt(0, CELL.title).textContent).toContain('Azure 系統管理');
    });

    it('reverts when the record has gone since the list was loaded', () => {
      initAndFlush();
      openEditor(courses[0], 'hour');

      api()['draft'].number = 35;
      api()['commit']();

      httpMock
        .expectOne(`${baseUrl}/1`)
        .flush('missing', { status: 404, statusText: 'Not Found' });

      expect(api()['courses']()[0].hour).toBe(21);
      expect(api()['editingCell']()).toBeNull();
    });
  });
});
