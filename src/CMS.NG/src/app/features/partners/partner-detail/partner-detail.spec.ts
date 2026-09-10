import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { PartnerDetail } from './partner-detail';
import { Partner } from '@core/models/partner.model';

describe('PartnerDetail', () => {
  let fixture: ComponentFixture<PartnerDetail>;
  let component: PartnerDetail;
  let httpMock: HttpTestingController;

  const partnerUrl = `${environment.apiUrl}/partners/1`;

  const partner: Partner = {
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
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(pkid: string | null = '1'): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(pkid ? { id: pkid } : {}) },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerDetail);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    // The 異動紀錄 badge this page renders fetches its own trail. That is the badge's own spec's
    // business, not this one's, so the request is answered here instead of in every case.
    httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]));
    httpMock.verify();
  });

  it('loads the record on init', async () => {
    await setup();
    fixture.detectChanges();

    httpMock.expectOne(partnerUrl).flush(partner);
    fixture.detectChanges();

    expect(api()['partner']()).toEqual(partner);
    expect(api()['loading']()).toBeFalse();
    expect(api()['notFound']()).toBeFalse();
  });

  it('renders every field', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(partnerUrl).flush(partner);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Microsoft');
    expect(text).toContain('MS');
    expect(text).toContain('微軟 Microsoft');
    expect(text).toContain('ms.png');
  });

  it('renders the five reference counts and the in-use note', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(partnerUrl).flush(partner);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('課程數');
    expect(text).toContain('認證數');
    expect(text).toContain('課程群組數');
    expect(text).toContain('活動數');
    expect(text).toContain('說明會數');
    expect(text).toContain('此原廠仍被引用，無法刪除。');
    expect(api()['referenceCount'](partner)).toBe(22);
  });

  it('omits the in-use note when nothing references the partner', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(partnerUrl).flush({
      ...partner,
      certificationCount: 0,
      courseCount: 0,
      courseGroupCount: 0,
      promotionCount: 0,
      seminarCount: 0,
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('此原廠仍被引用');
  });

  it('shows a dash for a null image filename', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(partnerUrl).flush({ ...partner, imageFilename: null });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('—');
  });

  it('reports not-found when the record load fails', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(partnerUrl).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('查無此原廠。');
  });

  it('reports not-found without a request when the route has no id', async () => {
    await setup(null);
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates back and to the edit route', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(partnerUrl).flush(partner);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['back']();
    expect(navigate).toHaveBeenCalledWith(['/partners']);

    api()['edit']();
    expect(navigate).toHaveBeenCalledWith(['/partners', 1, 'edit']);
  });
});
