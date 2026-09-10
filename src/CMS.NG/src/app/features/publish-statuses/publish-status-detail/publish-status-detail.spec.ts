import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { PublishStatusDetail } from './publish-status-detail';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusDetail', () => {
  let fixture: ComponentFixture<PublishStatusDetail>;
  let component: PublishStatusDetail;
  let httpMock: HttpTestingController;

  const statusUrl = `${environment.apiUrl}/publish-statuses/2`;

  const status: PublishStatus = {
    pkid: 2,
    description: '已發布',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false,
    courseCount: 12,
    promotionCount: 4,
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(pkid: string | null = '2'): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
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

    fixture = TestBed.createComponent(PublishStatusDetail);
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

    httpMock.expectOne(statusUrl).flush(status);
    fixture.detectChanges();

    expect(api()['status']()).toEqual(status);
    expect(api()['loading']()).toBeFalse();
    expect(api()['notFound']()).toBeFalse();
  });

  it('renders the status fields', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(statusUrl).flush(status);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('檢視發布狀態');
    expect(text).toContain('已發布');
    expect(text).toContain('狀態說明');
  });

  it('renders the reference counts and the in-use notice', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(statusUrl).flush(status);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('使用狀況');
    expect(text).toContain('12');
    expect(text).toContain('4');
    expect(text).toContain('無法刪除');
  });

  it('omits the in-use notice when nothing references the status', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(statusUrl).flush({ ...status, courseCount: 0, promotionCount: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('無法刪除');
  });

  it('flags not found when the record request fails', async () => {
    await setup('99');
    fixture.detectChanges();

    httpMock
      .expectOne(`${environment.apiUrl}/publish-statuses/99`)
      .flush('missing', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('查無此發布狀態');
  });

  it('does not call the API when the route has no id', async () => {
    await setup(null);
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(api()['loading']()).toBeFalse();
  });

  it('navigates back to the list and to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(statusUrl).flush(status);

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['back']();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses']);

    api()['edit']();
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 2, 'edit']);
  });
});
