import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { environment } from '@env';
import { FeaturedPromoItemService } from './featured-promo-item.service';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
} from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemService', () => {
  let service: FeaturedPromoItemService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/featured-promo-items`;

  const item: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 10,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    trainingCenter: { pkid: 1, name: '台北', appKey: 'TPE', isDefault: true },
    promotion: {
      pkid: 10,
      promoCode: '20251204_SkillTrainAI',
      topic: '成為能AI協作的程式設計師',
      description: '轉職就業養成班',
    },
  };

  const request: FeaturedPromoItemRequest = {
    pkid: 0,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 2,
    promotionPkid: 11,
    topic: 'Google AI工具一次掌握',
    description: '不需技術基礎',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(FeaturedPromoItemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll() issues GET to the collection route', () => {
    let result: FeaturedPromoItem[] | undefined;
    service.getAll().subscribe((items) => (result = items));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([item]);

    expect(result).toEqual([item]);
  });

  it('query() POSTs the centre and week filter to /query', () => {
    let result: FeaturedPromoItem[] | undefined;
    service.query({ trainingCenterPkid: 1, weekOf: '2026-03-18' }).subscribe((i) => (result = i));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ trainingCenterPkid: 1, weekOf: '2026-03-18' });
    req.flush([item]);

    expect(result?.length).toBe(1);
  });

  it('query() sends an empty filter body when unfiltered', () => {
    service.query({}).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.body).toEqual({});
    req.flush([]);
  });

  it('getById() issues GET to the numeric record route', () => {
    let result: FeaturedPromoItem | undefined;
    service.getById(1).subscribe((i) => (result = i));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(item);

    expect(result?.promotion.promoCode).toBe('20251204_SkillTrainAI');
  });

  it('create() POSTs the request body to the collection route', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...item, pkid: 2, slot: 2 });
  });

  it('update() PUTs to the collection route with the key in the body', () => {
    service.update({ ...request, pkid: 1 }).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(item);
  });

  it('delete() issues DELETE to the numeric record route', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('moveUp() POSTs to the move-up action route', () => {
    let result: FeaturedPromoItem | undefined;
    service.moveUp(1).subscribe((i) => (result = i));

    const req = httpMock.expectOne(`${baseUrl}/1/move-up`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...item, slot: 1 });

    expect(result?.slot).toBe(1);
  });

  it('moveDown() POSTs to the move-down action route', () => {
    let result: FeaturedPromoItem | undefined;
    service.moveDown(1).subscribe((i) => (result = i));

    const req = httpMock.expectOne(`${baseUrl}/1/move-down`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...item, slot: 2 });

    expect(result?.slot).toBe(2);
  });
});
