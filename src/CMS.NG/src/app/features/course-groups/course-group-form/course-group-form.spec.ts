import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { CourseGroupForm } from './course-group-form';
import { CourseGroup } from '@core/models/course-group.model';

describe('CourseGroupForm', () => {
  let fixture: ComponentFixture<CourseGroupForm>;
  let component: CourseGroupForm;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/course-groups`;

  const courseGroup: CourseGroup = {
    pkid: 1,
    description: '雲端技術',
    courseCount: 12,
    partnerCourseGroupCount: 3,
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CourseGroupForm],
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

    fixture = TestBed.createComponent(CourseGroupForm);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Runs ngOnInit and satisfies the record load in edit mode. */
  function init(pkid: string | null, payload: CourseGroup = courseGroup): void {
    fixture.detectChanges();
    if (pkid) {
      httpMock.expectOne(`${baseUrl}/${pkid}`).flush(payload);
    }
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ---------- Add mode ----------

  describe('add mode', () => {
    beforeEach(async () => {
      await setup(null);
    });

    it('starts in add mode with an empty form and no key', () => {
      init(null);

      expect(api()['isEdit']()).toBeFalse();
      expect(api()['pkid']()).toBeNull();
      expect(api()['form'].getRawValue()).toEqual({ description: '' });
    });

    it('renders no 主代碼 field — pkid is IDENTITY', () => {
      init(null);

      expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('新增課程群組');
    });

    it('blocks the save while the required field is empty', () => {
      init(null);

      api()['save']();

      httpMock.expectNone(baseUrl);
      expect(api()['form'].controls.description.touched).toBeTrue();
      expect(api()['isInvalid']('description')).toBeTrue();
    });

    it('POSTs a trimmed request with pkid 0', () => {
      init(null);
      api()['form'].setValue({ description: '  網路安全  ' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ pkid: 0, description: '網路安全' });
      req.flush({ ...courseGroup, pkid: 2, description: '網路安全' });
    });

    it('navigates to the new record on success', () => {
      init(null);
      api()['form'].setValue({ description: '網路安全' });
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['save']();
      httpMock.expectOne(baseUrl).flush({ ...courseGroup, pkid: 7, description: '網路安全' });

      expect(navigate).toHaveBeenCalledWith(['/course-groups', 7]);
      expect(api()['saving']()).toBeFalse();
    });

    it('stays on the form when the save fails', () => {
      init(null);
      api()['form'].setValue({ description: '網路安全' });
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['save']();
      httpMock.expectOne(baseUrl).flush('boom', { status: 500, statusText: 'Server Error' });

      expect(navigate).not.toHaveBeenCalled();
      expect(api()['saving']()).toBeFalse();
    });

    it('navigates back to the list on cancel', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['cancel']();

      expect(navigate).toHaveBeenCalledWith(['/course-groups']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('1');
    });

    it('loads the record and patches the control', () => {
      init('1');

      expect(api()['isEdit']()).toBeTrue();
      expect(api()['form'].getRawValue()).toEqual({ description: '雲端技術' });
    });

    it('shows the key as static text rather than a control', () => {
      init('1');

      expect(api()['pkid']()).toBe(1);
      expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('主代碼 1');
      expect(fixture.nativeElement.textContent).toContain('編輯課程群組');
    });

    it('PUTs to the collection route with the key in the body', () => {
      init('1');
      api()['form'].patchValue({ description: '雲端運算' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.pkid).toBe(1);
      expect(req.request.body.description).toBe('雲端運算');
      req.flush({ ...courseGroup, description: '雲端運算' });
    });

    it('recovers when the record load fails', () => {
      fixture.detectChanges();
      httpMock.expectOne(`${baseUrl}/1`).flush(null, { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(api()['loading']()).toBeFalse();
      expect(api()['form'].getRawValue().description).toBe('');
    });
  });
});
