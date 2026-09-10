import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '@env';
import {
  Course,
  CourseCopyRequest,
  CourseQuery,
  CourseRequest,
} from '@core/models/course.model';

@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/courses`;

  getAll(): Observable<Course[]> {
    return this.http.get<Course[]>(this.baseUrl);
  }

  query(query: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.baseUrl}/query`, query);
  }

  /** pkid is numeric, so it needs no URL encoding (unlike the AppRole string key). */
  getById(pkid: number): Observable<Course> {
    return this.http.get<Course>(`${this.baseUrl}/${pkid}`);
  }

  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.baseUrl, request);
  }

  /** The key travels in the body, not the route. */
  update(request: CourseRequest): Observable<Course> {
    return this.http.put<Course>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }

  /** Duplicates the course under a new 簡介代碼; 409 when that code is already taken. */
  copy(pkid: number, request: CourseCopyRequest): Observable<Course> {
    return this.http.post<Course>(`${this.baseUrl}/${pkid}/copy`, request);
  }

  /**
   * Records that a 課程簡介 PDF was asked for. Changes no row and writes no 異動紀錄 — it exists so
   * that "is this button used at all" has an answer the client cannot give. Fire-and-forget: the
   * caller ignores the failure, because the export is worth more than the record of it.
   */
  logSheetExport(pkid: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${pkid}/sheet`, null);
  }
}
