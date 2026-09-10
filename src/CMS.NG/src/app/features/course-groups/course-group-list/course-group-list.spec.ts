import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { CourseGroupList } from './course-group-list';
import { CourseGroup } from '@core/models/course-group.model';

describe('CourseGroupList', () => {
  let fixture: ComponentFixture<CourseGroupList>;
  let component: CourseGroupList;
  let httpMock: HttpTestingController;
  const queryUrl = `${environment.apiUrl}/course-groups/query`;

  const courseGroups: CourseGroup[] = [
    { pkid: 1, description: '雲端技術', courseCount: 12, partnerCourseGroupCount: 3 },
    { pkid: 2, description: '網路安全', courseCount: 0, partnerCourseGroupCount: 0 },
  ];

  const unfiltered = { keyword: null, inUse: null };

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [CourseGroupList],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function initAndFlush(payload: CourseGroup[] = courseGroups): void {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  it('creates and loads the course group list on init', () => {
    initAndFlush();

    expect(component).toBeTruthy();
    expect(api()['courseGroups']()).toEqual(courseGroups);
    expect(api()['loading']()).toBeFalse();
  });

  it('renders one row per course group', () => {
    initAndFlush();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('雲端技術');
    expect(rows[1].textContent).toContain('網路安全');
  });

  it('shows the two reference counts as their own columns', () => {
    initAndFlush();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr')[0].querySelectorAll('td');
    expect(cells[2].textContent.trim()).toBe('12');
    expect(cells[3].textContent.trim()).toBe('3');
    expect(api()['referenceCount'](courseGroups[0])).toBe(15);
    expect(api()['referenceCount'](courseGroups[1])).toBe(0);
  });

  it('sends an unfiltered query body by default', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(courseGroups);
  });

  it('applies drawer filters, resets paging, and re-queries', () => {
    initAndFlush();
    api()['page'] = { first: 40, rows: 20 };

    api()['openFilterDrawer']();
    api()['draftFilters'] = { keyword: '網路', inUse: false };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: '網路', inUse: false });
    req.flush([courseGroups[1]]);

    expect(api()['page'].first).toBe(0);
    expect(api()['filterDrawerVisible']()).toBeFalse();
    expect(api()['courseGroups']().length).toBe(1);
  });

  it('treats inUse=false as an active filter', () => {
    initAndFlush();
    expect(api()['hasActiveFilters']).toBeFalse();

    api()['draftFilters'] = { ...unfiltered, inUse: false };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.inUse).toBeFalse();
    req.flush([courseGroups[1]]);

    expect(api()['hasActiveFilters']).toBeTrue();
    expect(api()['activeFilterCount']).toBe(1);
  });

  it('counts the applied filters for the 搜尋條件 badge', () => {
    initAndFlush();
    expect(api()['activeFilterCount']).toBe(0);

    api()['draftFilters'] = { keyword: '雲端', inUse: true };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([courseGroups[0]]);
    fixture.detectChanges();

    expect(api()['activeFilterCount']).toBe(2);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')?.textContent.trim()).toBe('2');
  });

  it('shows no badge when nothing is filtered', () => {
    initAndFlush();

    expect(fixture.nativeElement.querySelector('p-button .p-badge')).toBeNull();
  });

  it('clears filters back to an unfiltered query', () => {
    initAndFlush();
    api()['draftFilters'] = { keyword: '雲端', inUse: true };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([]);

    api()['clearFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(courseGroups);
    expect(api()['hasActiveFilters']).toBeFalse();
  });

  it('persists applied filters, sort, and page to session storage', () => {
    initAndFlush();

    api()['draftFilters'] = { keyword: '雲端', inUse: null };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([courseGroups[0]]);

    api()['onSort']({ field: 'description', order: -1 });
    api()['onPage']({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('course-group-list-filters')!).keyword).toBe('雲端');
    expect(JSON.parse(sessionStorage.getItem('course-group-list-sort')!)).toEqual({
      field: 'description',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('course-group-list-page')!)).toEqual({
      first: 20,
      rows: 50,
    });
  });

  it('restores persisted filters on init', () => {
    sessionStorage.setItem(
      'course-group-list-filters',
      JSON.stringify({ keyword: null, inUse: true }),
    );

    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: null, inUse: true });
    req.flush([courseGroups[0]]);
  });

  it('defaults the sort to pkid ascending', () => {
    initAndFlush();

    expect(api()['sort']).toEqual({ field: 'pkid', order: 1 });
  });

  it('empties the list when the query fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['courseGroups']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates to the add, view, and edit routes', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['add']();
    expect(navigate).toHaveBeenCalledWith(['/course-groups/new']);

    api()['view'](courseGroups[0]);
    expect(navigate).toHaveBeenCalledWith(['/course-groups', 1]);

    api()['edit'](courseGroups[0]);
    expect(navigate).toHaveBeenCalledWith(['/course-groups', 1, 'edit']);
  });

  it('warns in the confirmation message when the group is still referenced', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](courseGroups[0]);

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('<b>1</b>');
    expect(message).toContain('雲端技術');
    expect(message).toContain('12 筆課程');
    expect(message).toContain('3 筆原廠課程群組');
  });

  it('omits the warning when nothing references the group', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](courseGroups[1]);

    expect(confirm.calls.mostRecent().args[0].message).not.toContain('無法刪除');
  });

  it('escapes record text before it reaches the HTML confirm message', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete']({ ...courseGroups[1], description: '<img src=x>' });

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('&lt;img src=x&gt;');
    expect(message).not.toContain('<img');
  });

  it('deletes the group and reloads once the confirmation is accepted', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](courseGroups[1]);

    const deleteReq = httpMock.expectOne(`${environment.apiUrl}/course-groups/2`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([courseGroups[0]]);
    expect(api()['courseGroups']().length).toBe(1);
  });

  it('keeps the row when the API rejects the delete with 409', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](courseGroups[0]);

    httpMock
      .expectOne(`${environment.apiUrl}/course-groups/1`)
      .flush({ title: '課程群組仍被使用' }, { status: 409, statusText: 'Conflict' });

    // No reload is issued on failure.
    httpMock.expectNone(queryUrl);
    expect(api()['courseGroups']().length).toBe(2);
  });
});
