import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { CourseGroupDetail } from './course-group-detail';
import { CourseGroup } from '@core/models/course-group.model';

describe('CourseGroupDetail', () => {
  let fixture: ComponentFixture<CourseGroupDetail>;
  let component: CourseGroupDetail;
  let httpMock: HttpTestingController;

  const courseGroupUrl = `${environment.apiUrl}/course-groups/1`;

  const courseGroup: CourseGroup = {
    pkid: 1,
    description: '雲端技術',
    courseCount: 12,
    partnerCourseGroupCount: 3,
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(pkid: string | null = '1'): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseGroupDetail],
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

    fixture = TestBed.createComponent(CourseGroupDetail);
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

    httpMock.expectOne(courseGroupUrl).flush(courseGroup);
    fixture.detectChanges();

    expect(api()['courseGroup']()).toEqual(courseGroup);
    expect(api()['loading']()).toBeFalse();
    expect(api()['notFound']()).toBeFalse();
  });

  it('renders every field', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(courseGroupUrl).flush(courseGroup);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('主代碼');
    expect(text).toContain('群組說明');
    expect(text).toContain('雲端技術');
  });

  it('renders both reference counts and the in-use note', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(courseGroupUrl).flush(courseGroup);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('課程數');
    expect(text).toContain('原廠群組數');
    expect(text).toContain('此課程群組仍被引用，無法刪除。');
    expect(api()['referenceCount'](courseGroup)).toBe(15);
  });

  it('omits the in-use note when nothing references the group', async () => {
    await setup();
    fixture.detectChanges();
    httpMock
      .expectOne(courseGroupUrl)
      .flush({ ...courseGroup, courseCount: 0, partnerCourseGroupCount: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('此課程群組仍被引用');
  });

  it('reports not-found when the record load fails', async () => {
    await setup();
    fixture.detectChanges();
    httpMock.expectOne(courseGroupUrl).flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(api()['notFound']()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('查無此課程群組。');
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
    httpMock.expectOne(courseGroupUrl).flush(courseGroup);
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);

    api()['back']();
    expect(navigate).toHaveBeenCalledWith(['/course-groups']);

    api()['edit']();
    expect(navigate).toHaveBeenCalledWith(['/course-groups', 1, 'edit']);
  });
});
