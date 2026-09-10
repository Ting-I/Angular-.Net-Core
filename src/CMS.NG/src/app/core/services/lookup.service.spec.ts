import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { LookupService } from './lookup.service';
import { AppUserLookup } from '@core/models/app-user-lookup.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';
import { PartnerLookup } from '@core/models/partner-lookup.model';
import { CourseGroupLookup } from '@core/models/course-group-lookup.model';
import { CertificationLookup } from '@core/models/certification-lookup.model';
import { JobCategoryLookup } from '@core/models/job-category-lookup.model';
import { TrainingCenterLookup } from '@core/models/training-center-lookup.model';
import { PromotionLookup } from '@core/models/promotion-lookup.model';

describe('LookupService', () => {
  let service: LookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(LookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAppUsers() issues GET to the app-users lookup route', () => {
    let result: AppUserLookup[] | undefined;
    service.getAppUsers().subscribe((users) => (result = users));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/app-users`);
    expect(req.request.method).toBe('GET');
    req.flush([{ userId: 'helen', userName: 'helen', isActive: true }]);

    expect(result?.length).toBe(1);
  });

  it('getAppRoles() issues GET to the app-roles lookup route', () => {
    let result: AppRoleLookup[] | undefined;
    service.getAppRoles().subscribe((roles) => (result = roles));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/app-roles`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1 },
      { roleId: 'User', roleName: 'User', permissionLevel: 100 },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[0].roleName).toBe('Administrator');
    expect(result?.[1].permissionLevel).toBe(100);
  });

  it('getPublishStatuses() issues GET to the publish-statuses lookup route', () => {
    let result: PublishStatusLookup[] | undefined;
    service.getPublishStatuses().subscribe((statuses) => (result = statuses));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/publish-statuses`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { pkid: 1, description: '草稿' },
      { pkid: 2, description: '已發布' },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[1].description).toBe('已發布');
  });

  it('getPartners() issues GET to the partners lookup route', () => {
    let result: PartnerLookup[] | undefined;
    service.getPartners().subscribe((partners) => (result = partners));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/partners`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { pkid: 1, name: 'Microsoft', appKey: 'MS' },
      { pkid: 2, name: 'Cisco', appKey: 'CSCO' },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[1].name).toBe('Cisco');
    expect(result?.[1].appKey).toBe('CSCO');
  });

  it('getCourseGroups() issues GET to the course-groups lookup route', () => {
    let result: CourseGroupLookup[] | undefined;
    service.getCourseGroups().subscribe((courseGroups) => (result = courseGroups));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/course-groups`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { pkid: 2, description: '網路安全' },
      { pkid: 1, description: '雲端技術' },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[1].description).toBe('雲端技術');
  });

  it('getCertifications() issues GET to the certifications lookup route', () => {
    let result: CertificationLookup[] | undefined;
    service.getCertifications().subscribe((certifications) => (result = certifications));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/certifications`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { pkid: 7, title: 'Azure Administrator', partnerPkid: 1, partnerName: 'Microsoft' },
      { pkid: 9, title: 'CCNA', partnerPkid: 2, partnerName: 'Cisco' },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[0].partnerName).toBe('Microsoft');
    expect(result?.[1].title).toBe('CCNA');
  });

  it('getJobCategories() issues GET to the job-categories lookup route', () => {
    let result: JobCategoryLookup[] | undefined;
    service.getJobCategories().subscribe((jobCategories) => (result = jobCategories));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/job-categories`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { pkid: 3, description: '系統管理' },
      { pkid: 5, description: '軟體開發' },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[1].description).toBe('軟體開發');
  });

  it('getTrainingCenters() issues GET to the training-centers lookup route', () => {
    let result: TrainingCenterLookup[] | undefined;
    service.getTrainingCenters().subscribe((centres) => (result = centres));

    const req = httpMock.expectOne(`${environment.apiUrl}/lookups/training-centers`);
    expect(req.request.method).toBe('GET');
    req.flush([
      { pkid: 1, name: '台北', appKey: 'TPE', isDefault: true },
      { pkid: 2, name: '新竹', appKey: 'HSC', isDefault: false },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[0].isDefault).toBeTrue();
    expect(result?.[1].name).toBe('新竹');
  });

  it('searchPromotions() issues GET to the promotions lookup route with the keyword', () => {
    let result: PromotionLookup[] | undefined;
    service.searchPromotions('2025').subscribe((promotions) => (result = promotions));

    const req = httpMock.expectOne(
      (r) =>
        r.url === `${environment.apiUrl}/lookups/promotions` && r.params.get('keyword') === '2025',
    );
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        pkid: 12,
        promoCode: '20251215_n8n',
        topic: 'n8n自動化三部曲',
        description: '從自動化新手',
      },
      { pkid: 10, promoCode: '20251204_SkillTrainAI', topic: '成為能AI協作', description: '轉職' },
    ]);

    expect(result?.length).toBe(2);
    expect(result?.[0].promoCode).toBe('20251215_n8n');
  });

  it('getPromotionByCode() issues GET to the encoded promo-code route', () => {
    let result: PromotionLookup | undefined;
    service.getPromotionByCode('2026/03 Promo&1').subscribe((promotion) => (result = promotion));

    const req = httpMock.expectOne(
      `${environment.apiUrl}/lookups/promotions/${encodeURIComponent('2026/03 Promo&1')}`,
    );
    expect(req.request.method).toBe('GET');
    req.flush({ pkid: 11, promoCode: '2026/03 Promo&1', topic: 'T', description: 'D' });

    expect(result?.pkid).toBe(11);
  });
});
