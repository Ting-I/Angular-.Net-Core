import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { CourseForm } from './course-form';
import { Course } from '@core/models/course.model';

describe('CourseForm', () => {
  let fixture: ComponentFixture<CourseForm>;
  let component: CourseForm;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/courses`;
  const lookupUrl = `${environment.apiUrl}/lookups`;

  const course: Course = {
    pkid: 1,
    title: 'Azure 系統管理',
    officialTitle: 'Microsoft Azure Administrator',
    courseId: 'AZ-104',
    prodCourseId: 'P-AZ-104',
    friendlyUrl: 'az-104',
    displayOrder: 3,
    partnerPkid: 1,
    courseGroupPkid: 2,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    // Deliberately NOT ScheduleOn + 10 years, so the auto-default cannot fake this passing.
    scheduleOff: '2027-06-30',
    hour: 21,
    listPrice: 24000,
    learningCredit: 3.5,
    material: '官方教材',
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
    certificationPkids: [7, 9],
    jobCategoryPkids: [3],
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseForm],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(pkid ? { id: pkid } : {}) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseForm);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Satisfies the five lookups (and, in edit mode, the record) the form forkJoins on init. */
  function init(pkid: string | null, payload: Course | null = course): void {
    fixture.detectChanges();

    httpMock.expectOne(`${lookupUrl}/partners`).flush([
      { pkid: 1, name: 'Microsoft', appKey: 'MS' },
      { pkid: 2, name: 'Cisco', appKey: 'CSCO' },
    ]);
    httpMock.expectOne(`${lookupUrl}/course-groups`).flush([{ pkid: 2, description: '雲端技術' }]);
    httpMock.expectOne(`${lookupUrl}/publish-statuses`).flush([{ pkid: 2, description: '已上架' }]);
    httpMock
      .expectOne(`${lookupUrl}/certifications`)
      .flush([
        { pkid: 7, title: 'Azure Administrator', partnerPkid: 1, partnerName: 'Microsoft' },
        { pkid: 9, title: '', partnerPkid: 2, partnerName: 'Cisco' },
      ]);
    httpMock.expectOne(`${lookupUrl}/job-categories`).flush([{ pkid: 3, description: '系統管理' }]);

    if (pkid) {
      const req = httpMock.expectOne(`${baseUrl}/${pkid}`);
      if (payload) {
        req.flush(payload);
      } else {
        req.flush('missing', { status: 404, statusText: 'Not Found' });
      }
    }

    fixture.detectChanges();
  }

  /** A complete, valid set of values for the thirteen required controls. */
  function fillRequired(): void {
    api()['form'].patchValue({
      title: '  思科網路  ',
      courseId: '  CCNA  ',
      prodCourseId: '  P-CCNA  ',
      friendlyUrl: '  ccna  ',
      displayOrder: 2,
      partnerPkid: 2,
      publishStatusPkid: 2,
      scheduleOn: new Date(2026, 2, 1),
      scheduleOff: new Date(2036, 2, 1),
      hour: 35,
      listPrice: 38000,
      learningCredit: 5,
    });
  }

  afterEach(() => httpMock.verify());

  /**
   * The Save / Cancel bar is pinned with position: sticky on .sticky-toolbar rather than by any
   * component state, so the assertion is on the computed style — a refactor that drops the rule
   * fails here instead of only being visible by scrolling a long form in a browser.
   */
  function expectStickyToolbar(): void {
    const toolbar: HTMLElement = fixture.nativeElement.querySelector('.sticky-toolbar');
    expect(toolbar).withContext('the action toolbar renders').not.toBeNull();

    const style = getComputedStyle(toolbar);
    expect(style.position).toBe('sticky');
    // top cancels the wrapper's own padding-top: that padding is the bleed out over .app-main's
    // padding, and the negative inset is what lands the pinned box on the clip edge above it.
    expect(parseFloat(style.paddingTop)).toBeGreaterThan(0);
    expect(parseFloat(style.top))
      .withContext('top has to negate the bleed, or the form shows above the bar')
      .toBeCloseTo(-parseFloat(style.paddingTop), 1);
    // Stacked over the form cards, so a field can never scroll on top of the buttons.
    expect(style.zIndex).not.toBe('auto');
    expect(Number(style.zIndex)).toBeGreaterThan(0);

    // The buttons stay inside the pinned box — outside it they would scroll away with the form.
    const labels: string[] = Array.from(
      toolbar.querySelectorAll('.page-actions button'),
      (button) => (button as HTMLElement).textContent?.trim() ?? '',
    );
    expect(labels.length).toBe(2);
    expect(labels[0]).toContain('取消');
    expect(labels[1]).toContain('儲存');
  }

  // ---------- Pinned action toolbar ----------

  describe('sticky action toolbar', () => {
    it('pins the Save / Cancel bar on the add form', async () => {
      await setup(null);
      init(null);

      expectStickyToolbar();
    });

    it('pins the Save / Cancel bar on the edit form', async () => {
      await setup('1');
      init('1');

      expectStickyToolbar();
      // Still one bar, not a second one added alongside the 主代碼 heading.
      expect(fixture.nativeElement.querySelectorAll('.sticky-toolbar').length).toBe(1);
    });

    /**
     * position: sticky is inert unless an ancestor actually scrolls, which is why app.scss gives
     * .app-shell the viewport height and lets .app-main scroll inside it. This stands in for that
     * ancestor — padding included, because that padding is the subtle part: the scroll container
     * clips at its padding box but pins sticky children to its content box, so the strip between
     * the two shows the form scrolling past above the bar unless the bar is bled out over it.
     */
    it('holds the toolbar against the top edge of a scrolling content region', async () => {
      await setup(null);
      init(null);

      const scroller = document.createElement('div');
      scroller.style.cssText = 'height: 300px; overflow: auto; padding: 20px;';
      document.body.appendChild(scroller);
      scroller.appendChild(fixture.nativeElement);
      fixture.detectChanges();

      const toolbar: HTMLElement = fixture.nativeElement.querySelector('.sticky-toolbar');
      const bar: HTMLElement = toolbar.querySelector('.page-header')!;
      try {
        expect(scroller.scrollHeight)
          .withContext('the form has to overflow, or nothing is being tested')
          .toBeGreaterThan(scroller.clientHeight + 400);

        // No border on the scroller, so this is both its clip edge and its padding-box top.
        const clipEdge = () => scroller.getBoundingClientRect().top;
        const restingOffset = bar.getBoundingClientRect().top - clipEdge();

        scroller.scrollTop = 400;

        const above = toolbar.getBoundingClientRect().top - clipEdge();
        expect(Math.abs(above))
          .withContext(`${above}px of the form was left showing above the pinned toolbar`)
          .toBeLessThan(1.5);

        // And it pins where it already sat, rather than dropping by the container's padding.
        const moved = bar.getBoundingClientRect().top - clipEdge() - restingOffset;
        expect(Math.abs(moved))
          .withContext(`the action bar jumped ${moved}px when it pinned`)
          .toBeLessThan(1.5);
      } finally {
        scroller.remove();
      }
    });
  });

  // ---------- Add mode ----------

  describe('add mode', () => {
    beforeEach(async () => {
      await setup(null);
    });

    it('starts in add mode with no key and no record fetch', () => {
      init(null);

      expect(api()['isEdit']()).toBeFalse();
      expect(api()['pkid']()).toBeNull();
      expect(api()['loading']()).toBeFalse();
      expect(fixture.nativeElement.textContent).toContain('新增課程');
    });

    it('renders no 主代碼 field — pkid is IDENTITY', () => {
      init(null);

      expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
      expect(fixture.nativeElement.querySelector('.page-key')).toBeNull();
    });

    it('blocks the save while required fields are empty', () => {
      init(null);

      api()['save']();

      httpMock.expectNone(baseUrl);
      expect(api()['isInvalid']('title')).toBeTrue();
      expect(api()['isInvalid']('courseId')).toBeTrue();
      expect(api()['isInvalid']('partnerPkid')).toBeTrue();
      expect(api()['isInvalid']('publishStatusPkid')).toBeTrue();
      expect(api()['isInvalid']('scheduleOn')).toBeTrue();
    });

    it('POSTs a trimmed request with pkid 0 and both pkid arrays', () => {
      init(null);
      fillRequired();
      api()['form'].patchValue({ certificationPkids: [7], jobCategoryPkids: [3] });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.pkid).toBe(0);
      expect(req.request.body.title).toBe('思科網路');
      expect(req.request.body.courseId).toBe('CCNA');
      expect(req.request.body.friendlyUrl).toBe('ccna');
      expect(req.request.body.certificationPkids).toEqual([7]);
      expect(req.request.body.jobCategoryPkids).toEqual([3]);
      req.flush({ ...course, pkid: 5, title: '思科網路' });
    });

    it('serialises the dates with local components', () => {
      init(null);
      fillRequired();
      // Late evening: toISOString() would report 2026-02-28 for a UTC+8 client.
      api()['form'].patchValue({ scheduleOn: new Date(2026, 2, 1, 23, 30) });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.body.scheduleOn).toBe('2026-03-01');
      req.flush({ ...course, pkid: 5 });
    });

    it('normalises blank optional fields to null', () => {
      init(null);
      fillRequired();
      api()['form'].patchValue({ officialTitle: '   ', note: '', outline: '  ' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.body.officialTitle).toBeNull();
      expect(req.request.body.note).toBeNull();
      expect(req.request.body.outline).toBeNull();
      req.flush({ ...course, pkid: 5 });
    });

    it('sends a null course group when none is selected', () => {
      init(null);
      fillRequired();

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.body.courseGroupPkid).toBeNull();
      req.flush({ ...course, pkid: 5 });
    });

    /** Picking 上架日期 defaults 下架日期 ten years on. */
    it('defaults scheduleOff to scheduleOn plus ten years', () => {
      init(null);

      api()['form'].controls.scheduleOn.setValue(new Date(2026, 0, 1));

      const scheduleOff = api()['form'].controls.scheduleOff.value as Date;
      expect(scheduleOff.getFullYear()).toBe(2036);
      expect(scheduleOff.getMonth()).toBe(0);
      expect(scheduleOff.getDate()).toBe(1);
    });

    it('navigates to the new record on success', () => {
      init(null);
      fillRequired();
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['save']();
      httpMock.expectOne(baseUrl).flush({ ...course, pkid: 7, title: '思科網路' });

      expect(navigate).toHaveBeenCalledWith(['/courses', 7]);
      expect(api()['saving']()).toBeFalse();
    });

    it('stays on the form when the save fails', () => {
      init(null);
      fillRequired();
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['save']();
      httpMock.expectOne(baseUrl).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(navigate).not.toHaveBeenCalled();
      expect(api()['saving']()).toBeFalse();
    });

    it('builds the certification options with a fallback for a blank title', () => {
      init(null);

      expect(api()['certificationOptions']).toEqual([
        { pkid: 7, label: 'Microsoft — Azure Administrator' },
        { pkid: 9, label: 'Cisco — 認證 #9' },
      ]);
    });

    it('cancels back to the list', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['cancel']();

      expect(navigate).toHaveBeenCalledWith(['/courses']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('1');
    });

    it('loads the record and shows the key as static text', () => {
      init('1');

      expect(api()['isEdit']()).toBeTrue();
      expect(api()['pkid']()).toBe(1);
      expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
      expect(fixture.nativeElement.querySelector('.page-key').textContent).toContain('主代碼 1');
      expect(fixture.nativeElement.textContent).toContain('編輯課程');
    });

    it('patches every scalar from the record', () => {
      init('1');
      const value = api()['form'].getRawValue();

      expect(value.title).toBe('Azure 系統管理');
      expect(value.courseId).toBe('AZ-104');
      expect(value.displayOrder).toBe(3);
      expect(value.partnerPkid).toBe(1);
      expect(value.courseGroupPkid).toBe(2);
      expect(value.hour).toBe(21);
      expect(value.listPrice).toBe(24000);
      expect(value.learningCredit).toBe(3.5);
      expect(value.canRepeat).toBeTrue();
      // Nullable columns come back as '' so the inputs stay controlled.
      expect(value.objective).toBe('');
    });

    /**
     * The scheduleOn patch fires the auto-default subscription; the stored scheduleOff is patched
     * afterwards and must win, or every edit would silently reset the course end date.
     */
    it('keeps the stored scheduleOff instead of the auto-defaulted one', () => {
      init('1');

      const scheduleOff = api()['form'].controls.scheduleOff.value as Date;
      expect(scheduleOff.getFullYear()).toBe(2027);
      expect(scheduleOff.getMonth()).toBe(5);
      expect(scheduleOff.getDate()).toBe(30);
    });

    it('patches both multiselects from the record', () => {
      init('1');
      const value = api()['form'].getRawValue();

      expect(value.certificationPkids).toEqual([7, 9]);
      expect(value.jobCategoryPkids).toEqual([3]);
    });

    it('PUTs to the collection route with the key in the body', () => {
      init('1');
      api()['form'].patchValue({ title: '改過的名稱' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.pkid).toBe(1);
      expect(req.request.body.title).toBe('改過的名稱');
      req.flush({ ...course, title: '改過的名稱' });
    });

    it('round-trips the dates unchanged when nothing else is edited', () => {
      init('1');

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.body.scheduleOn).toBe('2026-01-01');
      expect(req.request.body.scheduleOff).toBe('2027-06-30');
      req.flush(course);
    });

    it('recovers when the record fails to load', () => {
      init('1', null);

      expect(api()['loading']()).toBeFalse();
      expect(api()['pkid']()).toBeNull();
    });
  });
});
