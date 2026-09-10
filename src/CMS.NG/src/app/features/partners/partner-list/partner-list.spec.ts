import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { PartnerList } from './partner-list';
import { Partner } from '@core/models/partner.model';

describe('PartnerList', () => {
  let fixture: ComponentFixture<PartnerList>;
  let component: PartnerList;
  let httpMock: HttpTestingController;
  const queryUrl = `${environment.apiUrl}/partners/query`;

  const partners: Partner[] = [
    {
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
    },
    {
      pkid: 2,
      name: 'Cisco',
      appKey: 'CSCO',
      nameOnPartnerMenu: '思科 Cisco',
      nameOnCourseDetailPage: '思科',
      displayOrder: 2,
      imageFilename: null,
      certificationCount: 0,
      courseCount: 0,
      courseGroupCount: 0,
      promotionCount: 0,
      seminarCount: 0,
    },
  ];

  const unfiltered = { keyword: null, hasImage: null };

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function initAndFlush(payload: Partner[] = partners): void {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  it('creates and loads the partner list on init', () => {
    initAndFlush();

    expect(component).toBeTruthy();
    expect(api()['partners']()).toEqual(partners);
    expect(api()['loading']()).toBeFalse();
  });

  it('renders one row per partner', () => {
    initAndFlush();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Microsoft');
    expect(rows[0].textContent).toContain('ms.png');
    expect(rows[1].textContent).toContain('Cisco');
  });

  it('shows a dash instead of a blank image filename', () => {
    initAndFlush();

    const cells = fixture.nativeElement.querySelectorAll('tbody tr')[1].querySelectorAll('td');
    expect(cells[6].textContent.trim()).toBe('—');
  });

  it('sums the five child counts into the 引用數 column', () => {
    initAndFlush();

    expect(api()['referenceCount'](partners[0])).toBe(22);
    expect(api()['referenceCount'](partners[1])).toBe(0);

    const cells = fixture.nativeElement.querySelectorAll('tbody tr')[0].querySelectorAll('td');
    expect(cells[7].textContent.trim()).toBe('22');
  });

  it('breaks the counts down for the tooltip', () => {
    initAndFlush();

    const breakdown = api()['referenceBreakdown'](partners[0]) as string;
    expect(breakdown).toContain('課程 12');
    expect(breakdown).toContain('認證 4');
    expect(breakdown).toContain('課程群組 3');
    expect(breakdown).toContain('活動 2');
    expect(breakdown).toContain('說明會 1');
  });

  it('sends an unfiltered query body by default', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(partners);
  });

  it('applies drawer filters, resets paging, and re-queries', () => {
    initAndFlush();
    api()['page'] = { first: 40, rows: 20 };

    api()['openFilterDrawer']();
    api()['draftFilters'] = { keyword: 'Cisco', hasImage: false };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: 'Cisco', hasImage: false });
    req.flush([partners[1]]);

    expect(api()['page'].first).toBe(0);
    expect(api()['filterDrawerVisible']()).toBeFalse();
    expect(api()['partners']().length).toBe(1);
  });

  it('treats hasImage=false as an active filter', () => {
    initAndFlush();
    expect(api()['hasActiveFilters']).toBeFalse();

    api()['draftFilters'] = { ...unfiltered, hasImage: false };
    api()['applyFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.hasImage).toBeFalse();
    req.flush([partners[1]]);

    expect(api()['hasActiveFilters']).toBeTrue();
    expect(api()['activeFilterCount']).toBe(1);
  });

  it('counts the applied filters for the 搜尋條件 badge', () => {
    initAndFlush();
    expect(api()['activeFilterCount']).toBe(0);

    api()['draftFilters'] = { keyword: 'Microsoft', hasImage: true };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([partners[0]]);
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
    api()['draftFilters'] = { keyword: 'Cisco', hasImage: true };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([]);

    api()['clearFilters']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual(unfiltered);
    req.flush(partners);
    expect(api()['hasActiveFilters']).toBeFalse();
  });

  it('persists applied filters, sort, and page to session storage', () => {
    initAndFlush();

    api()['draftFilters'] = { keyword: 'Cisco', hasImage: null };
    api()['applyFilters']();
    httpMock.expectOne(queryUrl).flush([partners[1]]);

    api()['onSort']({ field: 'name', order: -1 });
    api()['onPage']({ first: 20, rows: 50 });

    expect(JSON.parse(sessionStorage.getItem('partner-list-filters')!).keyword).toBe('Cisco');
    expect(JSON.parse(sessionStorage.getItem('partner-list-sort')!)).toEqual({
      field: 'name',
      order: -1,
    });
    expect(JSON.parse(sessionStorage.getItem('partner-list-page')!)).toEqual({
      first: 20,
      rows: 50,
    });
  });

  it('restores persisted filters on init', () => {
    sessionStorage.setItem('partner-list-filters', JSON.stringify({ keyword: null, hasImage: true }));

    fixture.detectChanges();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ keyword: null, hasImage: true });
    req.flush([partners[0]]);
  });

  it('defaults the sort to DisplayOrder ascending', () => {
    initAndFlush();

    expect(api()['sort']).toEqual({ field: 'displayOrder', order: 1 });
  });

  it('empties the list when the query fails', () => {
    fixture.detectChanges();
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['partners']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates to the add, view, and edit routes', () => {
    initAndFlush();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['add']();
    expect(navigate).toHaveBeenCalledWith(['/partners/new']);

    api()['view'](partners[0]);
    expect(navigate).toHaveBeenCalledWith(['/partners', 1]);

    api()['edit'](partners[0]);
    expect(navigate).toHaveBeenCalledWith(['/partners', 1, 'edit']);
  });

  it('warns in the confirmation message when the partner is still referenced', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](partners[0]);

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('<b>1</b>');
    expect(message).toContain('Microsoft');
    expect(message).toContain('12 筆課程');
    expect(message).toContain('1 筆說明會');
  });

  it('omits the warning when nothing references the partner', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete'](partners[1]);

    expect(confirm.calls.mostRecent().args[0].message).not.toContain('無法刪除');
  });

  it('escapes record text before it reaches the HTML confirm message', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);

    api()['confirmDelete']({ ...partners[1], name: '<img src=x>' });

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('&lt;img src=x&gt;');
    expect(message).not.toContain('<img');
  });

  it('deletes the partner and reloads once the confirmation is accepted', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](partners[1]);

    const deleteReq = httpMock.expectOne(`${environment.apiUrl}/partners/2`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    httpMock.expectOne(queryUrl).flush([partners[0]]);
    expect(api()['partners']().length).toBe(1);
  });

  it('keeps the row when the API rejects the delete with 409', () => {
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](partners[0]);

    httpMock
      .expectOne(`${environment.apiUrl}/partners/1`)
      .flush({ title: '原廠仍被使用' }, { status: 409, statusText: 'Conflict' });

    // No reload is issued on failure.
    httpMock.expectNone(queryUrl);
    expect(api()['partners']().length).toBe(2);
  });
});
