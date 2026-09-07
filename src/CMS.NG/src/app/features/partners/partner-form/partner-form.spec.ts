import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { PartnerForm } from './partner-form';
import { Partner } from '@core/models/partner.model';

describe('PartnerForm', () => {
  let fixture: ComponentFixture<PartnerForm>;
  let component: PartnerForm;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/partners`;

  const partner: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: '微軟 Microsoft',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 5,
    imageFilename: 'ms.png',
    certificationCount: 4,
    courseCount: 12,
    courseGroupCount: 3,
    promotionCount: 2,
    seminarCount: 1,
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(pkid: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [PartnerForm],
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

    fixture = TestBed.createComponent(PartnerForm);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  }

  /** Runs ngOnInit and satisfies the record load in edit mode. */
  function init(pkid: string | null, payload: Partner = partner): void {
    fixture.detectChanges();
    if (pkid) {
      httpMock.expectOne(`${baseUrl}/${pkid}`).flush(payload);
    }
    fixture.detectChanges();
  }

  function fillValidForm(): void {
    api()['form'].setValue({
      name: 'Cisco',
      appKey: 'CSCO',
      nameOnPartnerMenu: '思科 Cisco',
      nameOnCourseDetailPage: '思科',
      displayOrder: 2,
      imageFilename: 'cisco.png',
    });
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
      expect(api()['form'].getRawValue()).toEqual({
        name: '',
        appKey: '',
        nameOnPartnerMenu: '',
        nameOnCourseDetailPage: '',
        displayOrder: 0,
        imageFilename: '',
      });
    });

    it('renders no 主代碼 field — pkid is IDENTITY', () => {
      init(null);

      expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('新增原廠');
    });

    it('blocks the save while required fields are empty', () => {
      init(null);

      api()['save']();

      httpMock.expectNone(baseUrl);
      expect(api()['form'].controls.name.touched).toBeTrue();
      expect(api()['isInvalid']('name')).toBeTrue();
      expect(api()['isInvalid']('appKey')).toBeTrue();
      expect(api()['isInvalid']('nameOnPartnerMenu')).toBeTrue();
      expect(api()['isInvalid']('nameOnCourseDetailPage')).toBeTrue();
    });

    it('POSTs a trimmed request with pkid 0', () => {
      init(null);
      fillValidForm();
      api()['form'].patchValue({ name: '  Cisco  ' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        pkid: 0,
        name: 'Cisco',
        appKey: 'CSCO',
        nameOnPartnerMenu: '思科 Cisco',
        nameOnCourseDetailPage: '思科',
        displayOrder: 2,
        imageFilename: 'cisco.png',
      });
      req.flush({ ...partner, pkid: 2, name: 'Cisco' });
    });

    it('normalises a blank image filename to null', () => {
      init(null);
      fillValidForm();
      api()['form'].patchValue({ imageFilename: '   ' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.body.imageFilename).toBeNull();
      req.flush({ ...partner, pkid: 2 });
    });

    it('navigates to the new record on success', () => {
      init(null);
      fillValidForm();
      const router = TestBed.inject(Router);
      const navigate = spyOn(router, 'navigate').and.resolveTo(true);

      api()['save']();
      httpMock.expectOne(baseUrl).flush({ ...partner, pkid: 7, name: 'Cisco' });

      expect(navigate).toHaveBeenCalledWith(['/partners', 7]);
      expect(api()['saving']()).toBeFalse();
    });

    it('stays on the form when the save fails', () => {
      init(null);
      fillValidForm();
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

      expect(navigate).toHaveBeenCalledWith(['/partners']);
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup('1');
    });

    it('loads the record and patches every control', () => {
      init('1');

      expect(api()['isEdit']()).toBeTrue();
      expect(api()['form'].getRawValue()).toEqual({
        name: 'Microsoft',
        appKey: 'MS',
        nameOnPartnerMenu: '微軟 Microsoft',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 5,
        imageFilename: 'ms.png',
      });
    });

    it('shows the key as static text rather than a control', () => {
      init('1');

      expect(api()['pkid']()).toBe(1);
      expect(fixture.nativeElement.querySelector('#pkid')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('主代碼 1');
      expect(fixture.nativeElement.textContent).toContain('編輯原廠');
    });

    it('patches a null image filename to an empty control value', () => {
      init('1', { ...partner, imageFilename: null });

      expect(api()['form'].getRawValue().imageFilename).toBe('');
    });

    it('PUTs to the collection route with the key in the body', () => {
      init('1');
      api()['form'].patchValue({ name: 'Microsoft Corp' });

      api()['save']();

      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.pkid).toBe(1);
      expect(req.request.body.name).toBe('Microsoft Corp');
      req.flush({ ...partner, name: 'Microsoft Corp' });
    });

    it('recovers when the record load fails', () => {
      fixture.detectChanges();
      httpMock.expectOne(`${baseUrl}/1`).flush(null, { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();

      expect(api()['loading']()).toBeFalse();
      expect(api()['form'].getRawValue().name).toBe('');
    });
  });
});
