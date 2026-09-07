import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { PublishStatusList } from './publish-status-list';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusList', () => {
  let fixture: ComponentFixture<PublishStatusList>;
  let component: PublishStatusList;
  let httpMock: HttpTestingController;
  const queryUrl = `${environment.apiUrl}/publish-statuses/query`;

  const statuses: PublishStatus[] = [
    {
      pkid: 1,
      description: '草稿',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
      courseCount: 0,
      promotionCount: 0,
    },
    {
      pkid: 2,
      description: '已發布',
      isDraft: false,
      isPublished: true,
      isDiscontinued: false,
      courseCount: 12,
      promotionCount: 4,
    },
  ];

  const unfiltered = {
    keyword: null,
    isDraft: null,
    isPublished: null,
    isDiscontinued: null,
  };

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function initAndFlush(payload: PublishStatus[] = statuses): void {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  it('creates and loads the status list on init', () => {
    initAndFlush();

    expect(component).toBeTruthy();
    expect(api()['statuses']()).toEqual(statuses);
    expect(api()['loading']()).toBeFalse();
  });

  it('renders one row per status with its reference counts', () => {
    initAndFlush();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[1].textContent).toContain('已發布');
    expect(rows[1].textContent).toContain('12');
    expect(rows[1].textContent).toContain('4');
  });

  it('renders the bit columns as check / minus icons', () => {
    initAndFlush();

    const draftRow = fixture.nativeElement.querySelectorAll('tbody tr')[0];
    expect(draftRow.querySelectorAll('i.pi-check').length).toBe(1);
    expect(draftRow.querySelectorAll('i.pi-minus').length).toBe(2);
  });

  it('sends an unfiltered query body by default', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(statuses);
  });

  it('applies drawer filters, resets paging, and re-queries', () => {
    initAndFlush();
    api()['page'] = { first: 40, rows: 20 };

    api()['openFilterDrawer']();
    api()['draftFilters'] = {
      keyword: '發布',
      isDraft: null,
      isPublished: true,
      isDiscontinued: null,
    };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({
      keyword: '發布',
      isDraft: null,
      isPublished: true,
      isDiscontinued: null,
    });
    req.flush([statuses[1]]);

    expect(api()['page'].first).toBe(0);
    expect(api()['filterDrawerVisible']()).toBeFalse();
    expect(api()['statuses']().length).toBe(1);
  });

  it('treats a false bool filter as an active filter', () => {
    initAndFlush();
    expect(api()['hasActiveFilters']).toBeFalse();

    api()['draftFilters'] = { ...unfiltered, isDraft: false };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.isDraft).toBeFalse();
    req.flush([statuses[1]]);

    expect(api()['hasActiveFilters']).toBeTrue();
  });

  it('counts the applied filters for the 搜尋條件 badge', () => {
    initAndFlush();
    expect(api()['activeFilterCount']).toBe(0);

    api()['draftFilters'] = { ...unfiltered, keyword: '發布', isPublished: true, isDraft: false };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([statuses[1]]);
    fixture.detectChanges();

    expect(api()['activeFilterCount']).toBe(3);
    expect(fixture.nativeElement.querySelector('p-button .p-badge')?.textContent.trim()).toBe('3');
  });

  it('shows no badge when nothing is filtered', () => {
    initAndFlush();

    expect(fixture.nativeElement.querySelector('p-button .p-badge')).toBeNull();
  });

  it('reports active filters when only a keyword is set', () => {
    initAndFlush();

    api()['draftFilters'] = { ...unfiltered, keyword: '草稿' };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([statuses[0]]);

    expect(api()['hasActiveFilters']).toBeTrue();
  });

  it('clears filters back to an unfiltered query', () => {
    initAndFlush();
    api()['draftFilters'] = { ...unfiltered, keyword: '草稿', isDraft: true };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([statuses[0]]);

    api()['clearFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(statuses);
    expect(api()['hasActiveFilters']).toBeFalse();
  });

  it('persists applied filters, sort, and page to session storage', () => {
    initAndFlush();

    api()['draftFilters'] = { ...unfiltered, keyword: '草稿' };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([statuses[0]]);

    api()['onSort']({ field: 'description', order: -1 });
    api()['onPage']({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-filters')!).keyword).toBe('草稿');
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-sort')!)).toEqual({
      field: 'description',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-page')!)).toEqual({
      first: 20,
      rows: 50,
    });
  });

  it('restores persisted filters on init', () => {
    sessionStorage.setItem(
      'publish-status-list-filters',
      JSON.stringify({ ...unfiltered, isPublished: true }),
    );

    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ ...unfiltered, isPublished: true });
    req.flush([statuses[1]]);
  });

  it('empties the list when the query fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['statuses']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates to the add, view, and edit routes', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['add']();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses/new']);

    api()['view'](statuses[0]);
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 1]);

    api()['edit'](statuses[0]);
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 1, 'edit']);
  });

  it('warns in the confirmation message when the status is still referenced', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](statuses[1]);

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('<b>2</b>');
    expect(message).toContain('已發布');
    expect(message).toContain('12 筆課程與 4 筆活動');
  });

  it('omits the warning when nothing references the status', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](statuses[0]);

    expect(confirm.calls.mostRecent().args[0].message).not.toContain('無法刪除');
  });

  it('deletes the status and reloads once the confirmation is accepted', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](statuses[0]);

    const deleteReq = httpMock.expectOne(`${environment.apiUrl}/publish-statuses/1`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([statuses[1]]);
    expect(api()['statuses']().length).toBe(1);
  });

  it('keeps the row when the API rejects the delete with 409', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](statuses[1]);

    httpMock
      .expectOne(`${environment.apiUrl}/publish-statuses/2`)
      .flush({ title: '發布狀態仍被使用' }, { status: 409, statusText: 'Conflict' });

    // No reload is issued on failure.
    httpMock.expectNone(queryUrl);
    expect(api()['statuses']().length).toBe(2);
  });
});
