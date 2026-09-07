import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { PublishStatusForm } from './publish-status-form';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusForm', () => {
  let fixture: ComponentFixture<PublishStatusForm>;
  let component: PublishStatusForm;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/publish-statuses`;

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

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PublishStatusForm],
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

    fixture = TestBed.createComponent(PublishStatusForm);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Runs ngOnInit and satisfies the record load in edit mode. */
  function init(pkid: string | null): void {
    fixture.detectChanges();
    if (pkid) {
      httpMock.expectOne(`${baseUrl}/${pkid}`).flush(status);
    }
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ---------- Add mode ----------

  describe('add mode', () => {
    beforeEach(async () => {
      await setup(null);
    });

    it('starts in add mode with every toggle off', () => {
      init(null);

      expect(api()['isEdit']()).toBeFalse();
      expect(component['form'].getRawValue()).toEqual({
        pkid: 0,
        description: '',
        isDraft: false,
        isPublished: false,
        isDiscontinued: false,
      });
      expect(fixture.nativeElement.textContent).toContain('新增發布狀態');
    });

    it('issues no HTTP request on init because there are no lookups', () => {
      init(null);

      httpMock.expectNone(baseUrl);
      expect(api()['loading']()).toBeFalse();
    });

    it('leaves the primary key editable', () => {
      init(null);

      expect(component['form'].controls.pkid.enabled).toBeTrue();
    });

    it('marks the form invalid until 狀態說明 is filled', () => {
      init(null);

      expect(component['form'].invalid).toBeTrue();

      component['form'].patchValue({ description: '審核中' });
      expect(component['form'].valid).toBeTrue();
    });

    it('rejects a pkid above the tinyint range', () => {
      init(null);

      component['form'].patchValue({ pkid: 256, description: '過大' });
      expect(component['form'].controls.pkid.invalid).toBeTrue();
    });

    it('does not POST when the form is invalid', () => {
      init(null);

      api()['save']();

      httpMock.expectNone(baseUrl);
      expect(component['form'].controls.description.touched).toBeTrue();
    });

    it('POSTs a trimmed request including the key and navigates to the new record', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      component['form'].patchValue({
        pkid: 4,
        description: '  審核中  ',
        isDraft: true,
      });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        pkid: 4,
        description: '審核中',
        isDraft: true,
        isPublished: false,
        isDiscontinued: false,
      });
      req.flush({ ...status, pkid: 4, description: '審核中' });

      expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 4]);
      expect(api()['saving']()).toBeFalse();
    });

    it('stops saving and stays on the form when the key conflicts', () => {
      init(null);
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      component['form'].patchValue({ pkid: 2, description: '重複' });
      api()['save']();

      httpMock
        .expectOne(baseUrl)
        .flush({ title: '主代碼已存在' }, { status: 409, statusText: 'Conflict' });

      expect(api()['saving']()).toBeFalse();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('navigates back to the list on cancel', () => {
      init(null);
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      api()['cancel']();

      expect(navigate).toHaveBeenCalledWith(['/publish-statuses']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('2');
    });

    it('loads the record and patches the form', () => {
      init('2');

      expect(api()['isEdit']()).toBeTrue();
      expect(component['form'].getRawValue()).toEqual({
        pkid: 2,
        description: '已發布',
        isDraft: false,
        isPublished: true,
        isDiscontinued: false,
      });
      expect(fixture.nativeElement.textContent).toContain('編輯發布狀態');
    });

    it('disables the primary key because it is immutable', () => {
      init('2');

      expect(component['form'].controls.pkid.disabled).toBeTrue();
    });

    it('PUTs the whole record including the disabled key', () => {
      init('2');
      const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      component['form'].patchValue({ description: '已上架', isDiscontinued: true });
      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        pkid: 2,
        description: '已上架',
        isDraft: false,
        isPublished: true,
        isDiscontinued: true,
      });
      req.flush({ ...status, description: '已上架' });

      expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 2]);
    });

    it('recovers when the record cannot be loaded', () => {
      fixture.detectChanges();
      httpMock
        .expectOne(`${baseUrl}/2`)
        .flush('missing', { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(api()['loading']()).toBeFalse();
      expect(component['form'].getRawValue().description).toBe('');
    });
  });
});
