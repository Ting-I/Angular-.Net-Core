import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '@env';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
} from '@core/models/featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/featured-promo-items`;

  getAll(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.baseUrl);
  }

  query(query: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.baseUrl}/query`, query);
  }

  /** pkid is numeric, so it needs no URL encoding (unlike AppRole's string key). */
  getById(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.baseUrl}/${pkid}`);
  }

  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.baseUrl, request);
  }

  /** The key travels in the body, not the route. */
  update(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.put<FeaturedPromoItem>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** The "-" action: 2 → 1, swapping with whatever sits on the target slot. */
  moveUp(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(`${this.baseUrl}/${pkid}/move-up`, null);
  }

  /** The "+" action: 1 → 2. */
  moveDown(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(`${this.baseUrl}/${pkid}/move-down`, null);
  }
}
