import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '@env';
import { AppUserLookup } from '@core/models/app-user-lookup.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';
import { PartnerLookup } from '@core/models/partner-lookup.model';
import { CourseGroupLookup } from '@core/models/course-group-lookup.model';
import { CertificationLookup } from '@core/models/certification-lookup.model';
import { JobCategoryLookup } from '@core/models/job-category-lookup.model';
import { TrainingCenterLookup } from '@core/models/training-center-lookup.model';
import { PromotionLookup } from '@core/models/promotion-lookup.model';

@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/lookups`;

  getAppUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.baseUrl}/app-users`);
  }

  getAppRoles(): Observable<AppRoleLookup[]> {
    return this.http.get<AppRoleLookup[]>(`${this.baseUrl}/app-roles`);
  }

  getPublishStatuses(): Observable<PublishStatusLookup[]> {
    return this.http.get<PublishStatusLookup[]>(`${this.baseUrl}/publish-statuses`);
  }

  getPartners(): Observable<PartnerLookup[]> {
    return this.http.get<PartnerLookup[]>(`${this.baseUrl}/partners`);
  }

  getCourseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.baseUrl}/course-groups`);
  }

  getCertifications(): Observable<CertificationLookup[]> {
    return this.http.get<CertificationLookup[]>(`${this.baseUrl}/certifications`);
  }

  getJobCategories(): Observable<JobCategoryLookup[]> {
    return this.http.get<JobCategoryLookup[]>(`${this.baseUrl}/job-categories`);
  }

  getTrainingCenters(): Observable<TrainingCenterLookup[]> {
    return this.http.get<TrainingCenterLookup[]>(`${this.baseUrl}/training-centers`);
  }

  /** Autocomplete feed: promotions whose PromoCode contains the keyword, newest first. */
  searchPromotions(keyword: string): Observable<PromotionLookup[]> {
    const params = new HttpParams().set('keyword', keyword);
    return this.http.get<PromotionLookup[]>(`${this.baseUrl}/promotions`, { params });
  }

  /** Exact PromoCode → Promotion2 row. The code is a string key, so it is URL-encoded. */
  getPromotionByCode(promoCode: string): Observable<PromotionLookup> {
    return this.http.get<PromotionLookup>(
      `${this.baseUrl}/promotions/${encodeURIComponent(promoCode)}`,
    );
  }
}
