import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '@env';
import { RowAuditEntry } from '@core/models/row-audit.model';

/**
 * 異動紀錄 RowAudit — the read side, and the only side. There is no create/update/delete here to
 * match the other services: the API has no such endpoints, by design.
 */
@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/rowaudit`;

  /**
   * One record's trail, newest first.
   *
   * `tableName` is the **database** table name — `Course`, `FeaturedPromoItem` — not the route
   * segment, and `pkid` is the surrogate key even for a record the operator knows by a string
   * (角色 AppRole, 使用者 AppUser): that is what the writer stored in PrimaryKeyValues.
   */
  getForRecord(tableName: string, pkid: number): Observable<RowAuditEntry[]> {
    const params = new HttpParams().set('tableName', tableName).set('pkid', pkid);
    return this.http.get<RowAuditEntry[]>(this.baseUrl, { params });
  }
}
