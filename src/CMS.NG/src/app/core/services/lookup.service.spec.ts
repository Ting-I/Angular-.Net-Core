import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { LookupService } from './lookup.service';
import { AppUserLookup } from '@core/models/app-user-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';

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
});
