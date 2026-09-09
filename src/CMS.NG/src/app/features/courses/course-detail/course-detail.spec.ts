import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { CourseDetail } from './course-detail';
import { Course } from '@core/models/course.model';
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

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseDetail],
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

    fixture = TestBed.createComponent(CourseDetail);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** The record and the two n-n label sources load in parallel. */
  function init(payload: Course | null = course, status = 200): void {
    fixture.detectChanges();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    if (payload && status === 200) {
      req.flush(payload);
    } else {
      req.flush('missing', { status: 404, statusText: 'Not Found' });
    }

    httpMock
      .expectOne(`${lookupUrl}/certifications`)
      .flush([{ pkid: 7, title: 'Azure Administrator', partnerPkid: 1, partnerName: 'Microsoft' }]);
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

      const hrefs = Array.from(
        fixture.nativeElement.querySelectorAll('a.detail-link') as NodeListOf<HTMLAnchorElement>,
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
        fixture.nativeElement.querySelectorAll('a.detail-link') as NodeListOf<HTMLAnchorElement>,
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
