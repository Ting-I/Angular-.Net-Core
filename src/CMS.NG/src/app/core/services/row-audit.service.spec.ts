import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { RowAuditService } from './row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

describe('RowAuditService', () => {
  let service: RowAuditService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/rowaudit`;

  const trail: RowAuditEntry[] = [
    {
      dateTime: '2026-06-04T14:30:00',
      userName: 'alice',
      actionType: 'Update',
      actionDesc: 'Title',
    },
    { dateTime: '2026-06-01T09:00:00', userName: 'bob', actionType: 'Insert', actionDesc: 'C-001' },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RowAuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('asks for one record by table name and pkid', () => {
    let received: RowAuditEntry[] | undefined;
    service.getForRecord('Course', 123).subscribe((entries) => (received = entries));

    const request = httpMock.expectOne((req) => req.url === baseUrl);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('tableName')).toBe('Course');
    expect(request.request.params.get('pkid')).toBe('123');

    request.flush(trail);
    expect(received).toEqual(trail);
  });

  it('passes the table name through as the database name, not a route segment', () => {
    service.getForRecord('FeaturedPromoItem', 3).subscribe();

    const request = httpMock.expectOne((req) => req.url === baseUrl);
    expect(request.request.params.get('tableName')).toBe('FeaturedPromoItem');
    request.flush([]);
  });
});
