import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';

import { environment } from '@env';
import { FeaturedPromoItemDraft, FeaturedPromoItemForm } from './featured-promo-item-form';
import { FeaturedPromoItem } from '@core/models/featured-promo-item.model';
import { PromotionLookup } from '@core/models/promotion-lookup.model';

describe('FeaturedPromoItemForm', () => {
  let fixture: ComponentFixture<FeaturedPromoItemForm>;
  let component: FeaturedPromoItemForm;
  let httpMock: HttpTestingController;
  let messageService: MessageService;

  const baseUrl = `${environment.apiUrl}/featured-promo-items`;
  const lookupUrl = `${environment.apiUrl}/lookups/promotions`;

  const skillTrainAi: PromotionLookup = {
    pkid: 10,
    promoCode: '20251204_SkillTrainAI',
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
  };

  const googleAi: PromotionLookup = {
    pkid: 11,
    promoCode: '251211_GoogleAI',
    topic: 'Google AI工具一次掌握',
    description: '不需技術基礎',
  };

  const item: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 2,
    promotionPkid: 10,
    topic: '自訂主題',
    description: '自訂說明',
    trainingCenter: { pkid: 1, name: '台北', appKey: 'TPE', isDefault: true },
    promotion: skillTrainAi,
  };

  const draft: FeaturedPromoItemDraft = {
    scheduleOn: '2026-03-18',
    trainingCenterPkid: 1,
    slot: 3,
  };

  const api = () => component as unknown as Record<string, any>;

  async function setup(inputs: { item?: FeaturedPromoItem | null; draft: FeaturedPromoItemDraft }) {
    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        MessageService,
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemForm);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('item', inputs.item ?? null);
    fixture.componentRef.setInput('draft', inputs.draft);
    httpMock = TestBed.inject(HttpTestingController);
    messageService = TestBed.inject(MessageService);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  // ---------- New mode ----------

  describe('new mode', () => {
    beforeEach(async () => {
      await setup({ draft });
    });

    it('starts empty with no resolved promotion', () => {
      expect(api()['isEdit']()).toBeFalse();
      expect(api()['promotion']()).toBeNull();
      expect(api()['form'].getRawValue()).toEqual({ promoCode: '', topic: '', description: '' });
    });

    it('blocks the save while required fields are empty', () => {
      api()['save']();

      httpMock.expectNone(baseUrl);
      httpMock.expectNone((r) => r.url.startsWith(lookupUrl));
      expect(api()['isInvalid']('promoCode')).toBeTrue();
      expect(api()['isInvalid']('topic')).toBeTrue();
      expect(api()['isInvalid']('description')).toBeTrue();
    });

    it('feeds the autocomplete from the promotions lookup', () => {
      api()['searchPromoCodes']({ originalEvent: new Event('input'), query: '2025' });

      const req = httpMock.expectOne(
        (r) => r.url === lookupUrl && r.params.get('keyword') === '2025',
      );
      req.flush([skillTrainAi, googleAi]);

      expect(api()['suggestions']()).toEqual(['20251204_SkillTrainAI', '251211_GoogleAI']);
    });

    it('resolves a selected suggestion and pre-fills the empty text fields', () => {
      api()['searchPromoCodes']({ originalEvent: new Event('input'), query: 'Google' });
      httpMock.expectOne((r) => r.url === lookupUrl).flush([googleAi]);

      api()['onPromoCodeSelected']({ originalEvent: new Event('click'), value: '251211_GoogleAI' });

      expect(api()['promotion']()).toEqual(googleAi);
      expect(api()['form'].getRawValue()).toEqual({
        promoCode: '251211_GoogleAI',
        topic: 'Google AI工具一次掌握',
        description: '不需技術基礎',
      });
    });

    it('查詢 resolves the typed code through the exact lookup and keeps text already entered', () => {
      api()['form'].setValue({ promoCode: '251211_GoogleAI', topic: '我的主題', description: '' });

      api()['lookupPromoCode']();

      httpMock.expectOne(`${lookupUrl}/251211_GoogleAI`).flush(googleAi);
      expect(api()['promotion']()?.pkid).toBe(11);
      expect(api()['form'].getRawValue().topic).toBe('我的主題');
      expect(api()['form'].getRawValue().description).toBe('不需技術基礎');
    });

    it('查詢 flags an unknown code without touching the text fields', () => {
      api()['form'].setValue({ promoCode: 'NOPE', topic: '主題', description: '說明' });
      const add = spyOn(messageService, 'add');

      api()['lookupPromoCode']();
      httpMock.expectOne(`${lookupUrl}/NOPE`).flush(null, { status: 404, statusText: 'Not Found' });

      expect(api()['promotion']()).toBeNull();
      expect(api()['form'].controls.promoCode.hasError('unknownPromoCode')).toBeTrue();
      expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ summary: '查無此活動代碼' }));
      expect(api()['form'].getRawValue().topic).toBe('主題');
    });

    it('resolves the code on save, then POSTs the draft position with pkid 0 and emits saved', () => {
      api()['form'].setValue({
        promoCode: ' 251211_GoogleAI ',
        topic: ' Google AI工具一次掌握 ',
        description: '不需技術基礎',
      });
      const saved = jasmine.createSpy('saved');
      component.saved.subscribe(saved);

      api()['save']();

      httpMock.expectOne(`${lookupUrl}/251211_GoogleAI`).flush(googleAi);
      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        pkid: 0,
        scheduleOn: '2026-03-18',
        trainingCenterPkid: 1,
        slot: 3,
        promotionPkid: 11,
        topic: 'Google AI工具一次掌握',
        description: '不需技術基礎',
      });
      const created = { ...item, pkid: 7, slot: 3, promotion: googleAi, promotionPkid: 11 };
      req.flush(created);

      expect(saved).toHaveBeenCalledWith(created);
      expect(api()['saving']()).toBeFalse();
    });

    it('does not save when the code is unknown', () => {
      api()['form'].setValue({ promoCode: 'NOPE', topic: '主題', description: '說明' });
      const saved = jasmine.createSpy('saved');
      component.saved.subscribe(saved);

      api()['save']();
      httpMock.expectOne(`${lookupUrl}/NOPE`).flush(null, { status: 404, statusText: 'Not Found' });

      httpMock.expectNone(baseUrl);
      expect(saved).not.toHaveBeenCalled();
      expect(api()['form'].controls.promoCode.hasError('unknownPromoCode')).toBeTrue();
      expect(api()['saving']()).toBeFalse();
    });

    it('reports a 409 when the slot was filled meanwhile and stays open', () => {
      api()['form'].setValue({ promoCode: '251211_GoogleAI', topic: '主題', description: '說明' });
      const add = spyOn(messageService, 'add');
      const saved = jasmine.createSpy('saved');
      component.saved.subscribe(saved);

      api()['save']();
      httpMock.expectOne(`${lookupUrl}/251211_GoogleAI`).flush(googleAi);
      httpMock
        .expectOne(baseUrl)
        .flush({ title: '版位已被使用' }, { status: 409, statusText: 'Conflict' });

      expect(saved).not.toHaveBeenCalled();
      expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ summary: '儲存失敗' }));
      expect(api()['saving']()).toBeFalse();
    });

    it('emits cancelled on cancel', () => {
      const cancelled = jasmine.createSpy('cancelled');
      component.cancelled.subscribe(cancelled);

      api()['cancel']();

      expect(cancelled).toHaveBeenCalled();
    });
  });

  // ---------- Paste (new mode with copied values) ----------

  describe('paste', () => {
    it('pre-fills the copied values and saves without a second lookup', async () => {
      await setup({
        draft: {
          ...draft,
          promoCode: skillTrainAi.promoCode,
          promotionPkid: skillTrainAi.pkid,
          topic: '複製的主題',
          description: '複製的說明',
        },
      });

      expect(api()['form'].getRawValue()).toEqual({
        promoCode: '20251204_SkillTrainAI',
        topic: '複製的主題',
        description: '複製的說明',
      });
      expect(api()['promotion']()?.pkid).toBe(10);

      api()['save']();

      httpMock.expectNone((r) => r.url.startsWith(lookupUrl));
      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('POST');
      expect(req.request.body.promotionPkid).toBe(10);
      expect(req.request.body.slot).toBe(3);
      req.flush({ ...item, pkid: 9 });
    });
  });

  // ---------- Edit mode ----------

  describe('edit mode', () => {
    beforeEach(async () => {
      await setup({ item, draft: { scheduleOn: item.scheduleOn, trainingCenterPkid: 1, slot: 2 } });
    });

    it('patches the existing values and the resolved promotion', () => {
      expect(api()['isEdit']()).toBeTrue();
      expect(api()['form'].getRawValue()).toEqual({
        promoCode: '20251204_SkillTrainAI',
        topic: '自訂主題',
        description: '自訂說明',
      });
      expect(api()['promotion']()).toEqual(skillTrainAi);
    });

    it('PUTs with the existing key and position, skipping the lookup for an unchanged code', () => {
      api()['form'].patchValue({ topic: '改過的主題' });

      api()['save']();

      httpMock.expectNone((r) => r.url.startsWith(lookupUrl));
      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({
        pkid: 1,
        scheduleOn: '2026-03-16',
        trainingCenterPkid: 1,
        slot: 2,
        promotionPkid: 10,
        topic: '改過的主題',
        description: '自訂說明',
      });
      req.flush({ ...item, topic: '改過的主題' });
    });

    it('re-resolves the code when it was changed, keeping the edited text', () => {
      api()['form'].patchValue({ promoCode: '251211_GoogleAI' });

      api()['save']();

      httpMock.expectOne(`${lookupUrl}/251211_GoogleAI`).flush(googleAi);
      const req = httpMock.expectOne(baseUrl);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.promotionPkid).toBe(11);
      expect(req.request.body.topic).toBe('自訂主題');
      req.flush({ ...item, promotionPkid: 11, promotion: googleAi });
    });
  });
});
