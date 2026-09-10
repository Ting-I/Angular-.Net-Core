import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideZoneChangeDetection } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { CourseDetail } from './course-detail';
import { Course } from '@core/models/course.model';
import { CertificationLookup } from '@core/models/certification-lookup.model';
import { PublishStatus } from '@core/models/publish-status.model';
import { qrPngDataUrl } from '@core/utils/qr-code.util';

describe('CourseDetail', () => {
  let fixture: ComponentFixture<CourseDetail>;
  let component: CourseDetail;
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
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: 2,
    publishStatusPkid: 3,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
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
    publishStatus: { pkid: 3, description: '已上架' },
    courseFaqCount: 4,
    courseRelatedLinkCount: 3,
    hotCourseCount: 2,
    courseRecommCount: 1,
    certificationPkids: [7],
    jobCategoryPkids: [3],
  };

  const api = () => component as unknown as Record<string, any>;

  /** What the lookup returns unless a spec needs a different answer. */
  const certificationLookup: CertificationLookup[] = [
    { pkid: 7, title: 'Azure Administrator', partnerPkid: 1, partnerName: 'Microsoft' },
  ];

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseDetail],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        // The real app coalesces events (app.config.ts), which defers the tick after an event to the
        // next animation frame. The print specs depend on that being true here too: without it the
        // zone's own tick would mask a deleted `detectChanges()` in the beforeprint handler.
        provideZoneChangeDetection({ eventCoalescing: true }),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(pkid ? { id: pkid } : {}) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseDetail);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** The record and the two n-n label sources load in parallel. */
  function init(
    payload: Course | null = course,
    status = 200,
    certifications: CertificationLookup[] = certificationLookup,
  ): void {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    if (payload && status === 200) {
      req.flush(payload);
    } else {
      req.flush('missing', { status: 404, statusText: 'Not Found' });
    }

    httpMock.expectOne(`${lookupUrl}/certifications`).flush(certifications);
    httpMock.expectOne(`${lookupUrl}/job-categories`).flush([{ pkid: 3, description: '系統管理' }]);

    fixture.detectChanges();
  }

  afterEach(() => {
    // The 異動紀錄 badge this page renders fetches its own trail. That is the badge's own spec's
    // business, not this one's, so the request is answered here instead of in every case.
    httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]));
    httpMock.verify();
  });

  describe('with a valid id', () => {
    beforeEach(async () => {
      await setup('1');
    });

    it('loads and renders the course', () => {
      init();

      expect(api()['course']()).toEqual(course);
      expect(api()['loading']()).toBeFalse();
      expect(api()['notFound']()).toBeFalse();
      expect(fixture.nativeElement.textContent).toContain('Azure 系統管理');
      expect(fixture.nativeElement.textContent).toContain('AZ-104');
    });

    it('links the three FK values to their own detail pages', () => {
      init();

      // Scoped to the cards on purpose: the page also hosts the print-only 課程簡介 sheet, whose own
      // footer link must stay free to be an ordinary <a> rather than carry this class forever.
      const hrefs = Array.from(
        fixture.nativeElement.querySelectorAll(
          'section.page-card a.detail-link',
        ) as NodeListOf<HTMLAnchorElement>,
      ).map((a) => a.getAttribute('href'));

      expect(hrefs).toEqual(['/partners/1', '/course-groups/2', '/publish-statuses/3']);
    });

    it('renders the resolved n-n names as chips', () => {
      init();

      expect(api()['certificationNames'](course)).toEqual(['Microsoft — Azure Administrator']);
      expect(api()['jobCategoryNames'](course)).toEqual(['系統管理']);
      expect(fixture.nativeElement.textContent).toContain('Azure Administrator');
      expect(fixture.nativeElement.textContent).toContain('系統管理');
    });

    it('renders the four child reference counts and the in-use note', () => {
      init();

      expect(api()['referenceCount'](course)).toBe(10);
      expect(fixture.nativeElement.textContent).toContain('此課程仍被引用，無法刪除。');
    });

    /**
     * 課程內容 is eight long-text columns, and those hold operator-pasted HTML for most courses.
     * The card interpolated all eight, so a real 課程大綱 rendered as a wall of visible <p> and <li>
     * tags on the page the operator checks the record on — the same defect the printed 課程簡介
     * fixed for the customer. `LongText` owns the rendering and `richText` the classification;
     * these cases pin that the card is actually wired to them, for every one of the eight.
     */
    describe('課程內容 long text', () => {
      /** A real 課程大綱 paste: a stray `</ul>`, an unclosed `<li>`, a `<font color>` vendor note. */
      const outlineHtml = [
        '<p>【網路基礎】</p>',
        '<ul style="list-style:disc">',
        '<li>網路基礎架構與網路服務</li>',
        '</ul>',
        '</ul>【藍隊資安防禦通識】</p>',
      ].join('\n');

      const contentCard = (): HTMLElement => fixture.nativeElement.querySelectorAll('section.page-card')[2];

      it('renders every one of the eight columns through app-long-text', () => {
        init();

        expect(contentCard().querySelector('h2')?.textContent).toContain('課程內容');
        expect(contentCard().querySelectorAll('app-long-text').length).toBe(8);
        // Nothing in the card interpolates a long-text column any more.
        expect(contentCard().querySelector('.detail-long-text')).toBeNull();
      });

      it('renders pasted markup as real elements rather than as visible tags', () => {
        init({ ...course, outline: outlineHtml });

        const rendered = contentCard().querySelector('.long-text-html') as HTMLElement;
        expect(rendered).not.toBeNull();
        expect(rendered.querySelectorAll('li').length).toBe(1);
        expect(rendered.textContent).toContain('網路基礎架構與網路服務');

        const cardText = contentCard().textContent as string;
        expect(cardText).not.toContain('<li>');
        expect(cardText).not.toContain('<p>');
        expect(cardText).not.toContain('list-style');
      });

      it('keeps a plain-text column on the plain path', () => {
        init({ ...course, material: '官方教材\n實作手冊' });

        const rendered = contentCard().querySelector('.long-text-plain') as HTMLElement;
        expect(rendered.textContent).toBe('官方教材\n實作手冊');
      });

      it('shows 「—」 for a column that is null or reads as blank', () => {
        init({ ...course, material: null, objective: '<p>&nbsp;</p>' });

        const bodies = Array.from(
          contentCard().querySelectorAll('app-long-text') as NodeListOf<HTMLElement>,
        ).map((el) => (el.textContent as string).trim());

        // The fixture leaves every long-text column null but the two set above, and both of those
        // read as blank too — so all eight fields show the placeholder and none shows a tag.
        expect(bodies).toEqual(new Array(8).fill('—'));
      });
    });
    it('navigates back and to the edit route', () => {
      init();
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['back']();
      expect(navigate).toHaveBeenCalledWith(['/courses']);

      api()['edit']();
      expect(navigate).toHaveBeenCalledWith(['/courses', 1, 'edit']);
    });

    it('shows the not-found state when the record is missing', () => {
      init(null, 404);

      expect(api()['notFound']()).toBeTrue();
      expect(fixture.nativeElement.textContent).toContain('查無此課程。');
    });

    it('omits the course-group link when the nullable FK is null', () => {
      init({ ...course, courseGroupPkid: null, courseGroup: null });

      const hrefs = Array.from(
        fixture.nativeElement.querySelectorAll(
          'section.page-card a.detail-link',
        ) as NodeListOf<HTMLAnchorElement>,
      ).map((a) => a.getAttribute('href'));

      expect(hrefs).toEqual(['/partners/1', '/publish-statuses/3']);
    });

    it('hides the in-use note when nothing references the course', () => {
      init({
        ...course,
        courseFaqCount: 0,
        courseRelatedLinkCount: 0,
        hotCourseCount: 0,
        courseRecommCount: 0,
      });

      expect(fixture.nativeElement.textContent).not.toContain('此課程仍被引用');
    });

    describe('QR code', () => {
      const publicUrl = 'https://www.uuu.com.tw/Course/Show/1/AZ-104';

      const figure = (): HTMLElement =>
        fixture.nativeElement.querySelector('section.page-card figure.qr-code');

      it('encodes the public course URL built from the record pkid and CourseId', () => {
        init();

        expect(api()['qrUrl']()).toBe(publicUrl);
        expect(figure().querySelector('img.qr-code-image')!.getAttribute('src')).toBe(
          qrPngDataUrl(publicUrl),
        );
      });

      it('takes the pkid and CourseId from the record, not the route', () => {
        init({ ...course, pkid: 42, courseId: 'AZ-900' });

        const expected = 'https://www.uuu.com.tw/Course/Show/42/AZ-900';
        expect(api()['qrUrl']()).toBe(expected);
        expect(figure().querySelector('img.qr-code-image')!.getAttribute('src')).toBe(
          qrPngDataUrl(expected),
        );
      });

      it('trims surrounding whitespace out of the encoded CourseId', () => {
        init({ ...course, courseId: 'AZ-104   ' });

        expect(api()['qrUrl']()).toBe(publicUrl);
      });

      it('shows the CourseId as the QR code title, inside 基本資料', () => {
        init();

        const section = figure().closest('section.page-card')!;
        expect(section.querySelector('h2')!.textContent!.trim()).toBe('基本資料');
        expect(figure().querySelector('.qr-code-title')!.textContent!.trim()).toBe('AZ-104');
      });

      it('downloads the QR code as a PNG image named after the CourseId', () => {
        init();

        const anchor = document.createElement('a');
        const click = spyOn(anchor, 'click');
        const create = document.createElement.bind(document);
        spyOn(document, 'createElement').and.callFake((tag: string) =>
          tag === 'a' ? anchor : create(tag),
        );

        api()['downloadQrCode']();

        expect(click).toHaveBeenCalled();
        expect(anchor.download).toBe('course-AZ-104-qrcode.png');

        const href = anchor.getAttribute('href')!;
        expect(href.startsWith('data:image/png;base64,')).toBeTrue();
        expect(atob(href.split(',')[1]).slice(0, 8)).toBe('\x89PNG\r\n\x1a\n');
      });

      it('renders no QR code when the record is missing', () => {
        init(null, 404);

        expect(api()['qrUrl']()).toBeNull();
        expect(api()['qrImage']()).toBeNull();
        expect(figure()).toBeNull();
      });
    });

    it('falls back to the pkid when a lookup does not resolve', () => {
      init({ ...course, certificationPkids: [99], jobCategoryPkids: [99] });

      expect(api()['certificationNames'](api()['course']())).toEqual(['認證 #99']);
      expect(api()['jobCategoryNames'](api()['course']())).toEqual(['職務 #99']);
    });

    /**
     * 課程簡介 PDF. Karma can see none of what this feature actually produces — margins, the page
     * stamp, font selection, page 2 — so these specs pin the logic around the print instead: what the
     * operator is warned about, what the document is called, what the sheet is handed, and that the
     * body class never outlives the print. The rendered page is T5's manual pass.
     */
    describe('課程簡介 PDF', () => {
      const statusUrl = `${environment.apiUrl}/publish-statuses`;
      const publicUrl = 'https://www.uuu.com.tw/Course/Show/1/AZ-104';

      const published: PublishStatus = {
        pkid: 3,
        description: '已上架',
        isDraft: false,
        isPublished: true,
        isDiscontinued: false,
        courseCount: 1,
        promotionCount: 0,
      };

      /** A window nothing can fall outside of, so an all-clear case never depends on today's date. */
      const openWindow: Partial<Course> = { scheduleOn: '2000-01-01', scheduleOff: '2099-12-31' };

      const openCourse = (overrides: Partial<Course> = {}): Course => ({
        ...course,
        ...openWindow,
        ...overrides,
      });

      const sheet = (): HTMLElement => fixture.nativeElement.querySelector('app-course-sheet');

      function confirmSpy(): jasmine.Spy {
        const service = fixture.debugElement.injector.get(ConfirmationService);
        return spyOn(service, 'confirm').and.returnValue(service);
      }

      /** Answers the two click-time re-reads; a null argument fails that fetch instead. */
      function flushGate(
        options: { course?: Course | null; status?: PublishStatus | null } = {},
      ): void {
        const record = options.course === undefined ? openCourse() : options.course;
        const courseReq = httpMock.expectOne(`${baseUrl}/1`);
        if (record) {
          courseReq.flush(record);
        } else {
          courseReq.flush('boom', { status: 500, statusText: 'Server Error' });
        }

        const status = options.status === undefined ? published : options.status;
        const statusReq = httpMock.expectOne(`${statusUrl}/3`);
        if (status) {
          statusReq.flush(status);
        } else {
          statusReq.flush('boom', { status: 500, statusText: 'Server Error' });
        }
      }

      /** The fire-and-forget export ping. */
      function flushPing(status = 204): void {
        const ping = httpMock.expectOne(`${baseUrl}/1/sheet`);
        expect(ping.request.method).toBe('POST');
        ping.flush(null, { status, statusText: status === 204 ? 'No Content' : 'Server Error' });
      }

      /** Runs `body` with the clock pinned, always uninstalling it — a leaked clock hangs PrimeNG. */
      function atDate(date: Date, body: () => void): void {
        jasmine.clock().install();
        try {
          jasmine.clock().mockDate(date);
          body();
        } finally {
          jasmine.clock().uninstall();
        }
      }

      describe('the click-time gate', () => {
        it('prints straight away when nothing is wrong, and asks nothing', () => {
          init(openCourse());
          const print = spyOn(window, 'print');
          const confirm = confirmSpy();

          api()['saveAsPdf']();
          flushGate();
          flushPing();

          expect(print).toHaveBeenCalledTimes(1);
          expect(confirm).not.toHaveBeenCalled();
        });

        it('does nothing on a page with no record — not even a request', () => {
          init(null, 404);
          const print = spyOn(window, 'print');

          api()['saveAsPdf']();

          httpMock.expectNone(`${baseUrl}/1`);
          httpMock.expectNone(`${statusUrl}/3`);
          expect(print).not.toHaveBeenCalled();
        });

        it('warns with the count when a certification did not resolve, and prints on accept', () => {
          init(openCourse({ certificationPkids: [7, 99] }));
          const print = spyOn(window, 'print');
          const confirm = confirmSpy();

          api()['saveAsPdf']();
          flushGate({ course: openCourse({ certificationPkids: [7, 99] }) });

          const options = confirm.calls.mostRecent().args[0];
          expect(confirm).toHaveBeenCalledTimes(1);
          expect(options.message as string).toContain('1 筆');
          expect(options.acceptLabel).toBe('仍要產生');
          expect(options.rejectLabel).toBe('取消');
          expect(options.defaultFocus).toBe('reject');
          expect(print).not.toHaveBeenCalled();

          options.accept!();
          flushPing();
          expect(print).toHaveBeenCalledTimes(1);
        });

        it('prints nothing and records nothing when the operator backs out', () => {
          init(openCourse({ certificationPkids: [99] }));
          const print = spyOn(window, 'print');
          confirmSpy();

          api()['saveAsPdf']();
          flushGate({ course: openCourse({ certificationPkids: [99] }) });

          expect(print).not.toHaveBeenCalled();
          httpMock.expectNone(`${baseUrl}/1/sheet`);
        });

        it('warns that the QR may not open when the course is not published', () => {
          init(openCourse());
          spyOn(window, 'print');
          const confirm = confirmSpy();

          api()['saveAsPdf']();
          flushGate({ status: { ...published, isPublished: false, isDraft: true } });

          expect(confirm.calls.mostRecent().args[0].message as string).toContain('未上架');
        });

        it('warns before the schedule window opens and after it closes', () => {
          init();
          spyOn(window, 'print');
          const confirm = confirmSpy();
          const scheduled = { ...course, scheduleOn: '2026-10-01', scheduleOff: '2026-10-31' };

          atDate(new Date(2026, 8, 9), () => {
            api()['saveAsPdf']();
            flushGate({ course: scheduled });
          });
          expect(confirm.calls.mostRecent().args[0].message as string).toContain('排程期間');

          atDate(new Date(2026, 10, 1), () => {
            api()['saveAsPdf']();
            flushGate({ course: scheduled });
          });
          expect(confirm.calls.count()).toBe(2);
          expect(confirm.calls.mostRecent().args[0].message as string).toContain('排程期間');
        });

        it('says nothing on either boundary day — 下架日期 is the last valid day', () => {
          init();
          const print = spyOn(window, 'print');
          const confirm = confirmSpy();
          const scheduled = { ...course, scheduleOn: '2026-10-01', scheduleOff: '2026-10-31' };

          atDate(new Date(2026, 9, 1), () => {
            api()['saveAsPdf']();
            flushGate({ course: scheduled });
            flushPing();
          });

          atDate(new Date(2026, 9, 31), () => {
            api()['saveAsPdf']();
            flushGate({ course: scheduled });
            flushPing();
          });

          expect(confirm).not.toHaveBeenCalled();
          expect(print).toHaveBeenCalledTimes(2);
        });

        it('fails open when the course re-read fails: warns, then prints the record on screen', () => {
          init(openCourse());
          const print = spyOn(window, 'print');
          const confirm = confirmSpy();

          api()['saveAsPdf']();
          flushGate({ course: null });

          const options = confirm.calls.mostRecent().args[0];
          expect(options.message as string).toContain('無法重新讀取');
          options.accept!();
          flushPing();

          expect(print).toHaveBeenCalledTimes(1);
          expect(api()['course']()).toEqual(openCourse());
        });

        it('fails open when the publish status cannot be read', () => {
          init(openCourse());
          const print = spyOn(window, 'print');
          const confirm = confirmSpy();

          api()['saveAsPdf']();
          flushGate({ status: null });

          const options = confirm.calls.mostRecent().args[0];
          expect(options.message as string).toContain('無法重新讀取');
          options.accept!();
          flushPing();
          expect(print).toHaveBeenCalledTimes(1);
        });

        it('raises one dialog carrying every reason that applies', () => {
          init();
          spyOn(window, 'print');
          const confirm = confirmSpy();

          atDate(new Date(2026, 8, 9), () => {
            api()['saveAsPdf']();
            flushGate({
              course: {
                ...course,
                certificationPkids: [99],
                scheduleOn: '2026-10-01',
                scheduleOff: '2026-10-31',
              },
              status: { ...published, isPublished: false },
            });
          });

          expect(confirm).toHaveBeenCalledTimes(1);
          const message = confirm.calls.mostRecent().args[0].message as string;
          expect(message).toContain('1 筆');
          expect(message).toContain('未上架');
          expect(message).toContain('排程期間');
        });

        it('takes the fresh copy of the record, so the sheet prints what is true now', () => {
          init(openCourse());
          spyOn(window, 'print');

          api()['saveAsPdf']();
          flushGate({ course: openCourse({ title: '改過的課程名稱' }) });
          flushPing();
          // In the browser the coalesced tick, and then beforeprint's own detectChanges(), render
          // this; a spec has to ask.
          fixture.detectChanges();

          expect(api()['course']()!.title).toBe('改過的課程名稱');
          expect(sheet().textContent).toContain('改過的課程名稱');
        });
      });

      describe('the export ping', () => {
        it('is fired once, and a failure from it does not stop the print', () => {
          init(openCourse());
          const print = spyOn(window, 'print');

          api()['saveAsPdf']();
          flushGate();

          expect(print).toHaveBeenCalledTimes(1);
          flushPing(500);
        });
      });

      describe('the print lifecycle', () => {
        let title: string;

        beforeEach(() => {
          title = document.title;
        });

        afterEach(() => {
          document.title = title;
          document.body.classList.remove('print-sheet');
        });

        it('names the document, stamps today on the sheet and claims the sheet page box', async () => {
          init();

          atDate(new Date(2026, 8, 9), () => {
            window.dispatchEvent(new Event('beforeprint'));

            // No manual detectChanges(): the handler has to do it itself, because the browser
            // snapshots the page before the coalesced tick would run.
            expect(document.title).toBe('課程簡介_AZ-104_20260909');
            expect(document.body.classList.contains('print-sheet')).toBeTrue();
            expect(sheet().textContent).toContain('2026/09/09');
          });

          await new Promise(requestAnimationFrame);
        });

        it('replaces characters a filename cannot carry, and falls back to the pkid', async () => {
          init({ ...course, courseId: 'AB/1:2' });

          atDate(new Date(2026, 8, 9), () => {
            window.dispatchEvent(new Event('beforeprint'));
            expect(document.title).toBe('課程簡介_AB-1-2_20260909');
          });

          await new Promise(requestAnimationFrame);
        });

        it('uses the pkid when the CourseId is blank', async () => {
          init({ ...course, pkid: 1, courseId: '  ' });

          atDate(new Date(2026, 8, 9), () => {
            window.dispatchEvent(new Event('beforeprint'));
            expect(document.title).toBe('課程簡介_1_20260909');
          });

          await new Promise(requestAnimationFrame);
        });

        it('restores the title and drops the body class on afterprint', async () => {
          init();

          window.dispatchEvent(new Event('beforeprint'));
          expect(document.title).not.toBe(title);

          window.dispatchEvent(new Event('afterprint'));
          expect(document.title).toBe(title);
          expect(document.body.classList.contains('print-sheet')).toBeFalse();

          await new Promise(requestAnimationFrame);
        });

        it('drops the body class on destroy too, so a print nobody finished cannot leak', async () => {
          init();

          window.dispatchEvent(new Event('beforeprint'));
          expect(document.body.classList.contains('print-sheet')).toBeTrue();

          fixture.destroy();

          expect(document.body.classList.contains('print-sheet')).toBeFalse();
          expect(document.title).toBe(title);

          await new Promise(requestAnimationFrame);
        });

        it('leaves the title alone on an empty state, and so does the afterprint that follows', async () => {
          init(null, 404);

          window.dispatchEvent(new Event('beforeprint'));
          expect(document.title).toBe(title);
          expect(document.body.classList.contains('print-sheet')).toBeFalse();

          window.dispatchEvent(new Event('afterprint'));
          expect(document.title).toBe(title);

          await new Promise(requestAnimationFrame);
        });
      });

      describe('what the sheet is handed', () => {
        it('renders the sheet and marks the host only once a record is loaded', () => {
          fixture.detectChanges();
          expect((fixture.nativeElement as HTMLElement).classList.contains('has-sheet')).toBeFalse();

          init();

          expect((fixture.nativeElement as HTMLElement).classList.contains('has-sheet')).toBeTrue();
          expect(sheet()).not.toBeNull();
        });

        it('renders no sheet on 查無此課程', () => {
          init(null, 404);

          expect((fixture.nativeElement as HTMLElement).classList.contains('has-sheet')).toBeFalse();
          expect(sheet()).toBeNull();
        });

        it('gives the sheet its own QR at print resolution, leaving the screen one alone', () => {
          init();

          expect(api()['sheetQrImage']()).toBe(qrPngDataUrl(publicUrl, { cellSize: 10 }));
          expect(api()['qrImage']()).toBe(qrPngDataUrl(publicUrl));
          expect(api()['sheetQrImage']()).not.toBe(api()['qrImage']());
        });

        it('drops unresolved certifications from the sheet while the screen keeps the placeholder', () => {
          init({ ...course, certificationPkids: [7, 99] });
          const record = api()['course']();

          expect(api()['certificationNames'](record)).toEqual([
            'Microsoft — Azure Administrator',
            '認證 #99',
          ]);
          expect(api()['sheetCertificationNames'](record)).toEqual(['Azure Administrator']);
        });

        it('drops a certification the lookup names with a blank title', () => {
          init(course, 200, [{ pkid: 7, title: '', partnerPkid: 1, partnerName: 'Microsoft' }]);
          const record = api()['course']();

          expect(api()['certificationNames'](record)).toEqual(['Microsoft — 認證 #7']);
          expect(api()['sheetCertificationNames'](record)).toEqual([]);
        });

        it('drops every certification when the lookup itself came back empty', () => {
          init(course, 200, []);
          const record = api()['course']();

          expect(api()['certificationNames'](record)).toEqual(['認證 #7']);
          expect(api()['sheetCertificationNames'](record)).toEqual([]);
        });

        it('prints the title alone for the course own vendor and prefixes another one', () => {
          init();
          expect(api()['sheetCertificationNames'](api()['course']())).toEqual([
            'Azure Administrator',
          ]);

          // Same lookup row, a course from a different vendor: the 原廠 in the facts grid is no longer
          // the certification's, so the distinction is worth the words. Compared by pkid, never name —
          // a null `partner` on the course must not turn into a prefix on every row.
          expect(
            api()['sheetCertificationNames']({ ...course, partnerPkid: 2, partner: null }),
          ).toEqual(['Microsoft — Azure Administrator']);
        });
      });
    });
  });

  describe('without an id', () => {
    beforeEach(async () => {
      await setup(null);
    });

    it('shows the not-found state without calling the API', () => {
      fixture.detectChanges();

      httpMock.expectNone(`${baseUrl}/1`);
      expect(api()['notFound']()).toBeTrue();
      expect(api()['loading']()).toBeFalse();
    });
  });
});
