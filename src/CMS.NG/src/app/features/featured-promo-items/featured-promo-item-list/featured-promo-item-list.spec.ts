import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService } from 'primeng/api';

import { environment } from '@env';
import { FeaturedPromoItemList } from './featured-promo-item-list';
import { FeaturedPromoItem } from '@core/models/featured-promo-item.model';
import { TrainingCenterLookup } from '@core/models/training-center-lookup.model';
import { startOfWeek, toIso } from '@core/utils/date.util';

describe('FeaturedPromoItemList', () => {
  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let component: FeaturedPromoItemList;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiUrl}/featured-promo-items`;
  const queryUrl = `${baseUrl}/query`;
  const centresUrl = `${environment.apiUrl}/lookups/training-centers`;
  const FILTERS_KEY = 'featured-promo-item-list-filters';

  const taipei: TrainingCenterLookup = { pkid: 1, name: '台北', appKey: 'TPE', isDefault: true };
  const hsinchu: TrainingCenterLookup = { pkid: 2, name: '新竹', appKey: 'HSC', isDefault: false };
  const centres = [taipei, hsinchu];

  const skillTrainAi = {
    pkid: 10,
    promoCode: '20251204_SkillTrainAI',
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
  };
  const googleAi = {
    pkid: 11,
    promoCode: '251211_GoogleAI',
    topic: 'Google AI工具一次掌握',
    description: '不需技術基礎',
  };

  /** 2026-03-16 is a Monday; the week runs to Sunday 2026-03-22. */
  const items: FeaturedPromoItem[] = [
    {
      pkid: 1,
      scheduleOn: '2026-03-16',
      trainingCenterPkid: 1,
      slot: 1,
      promotionPkid: 10,
      topic: skillTrainAi.topic,
      description: skillTrainAi.description,
      trainingCenter: taipei,
      promotion: skillTrainAi,
    },
    {
      pkid: 2,
      scheduleOn: '2026-03-16',
      trainingCenterPkid: 1,
      slot: 2,
      promotionPkid: 11,
      topic: googleAi.topic,
      description: googleAi.description,
      trainingCenter: taipei,
      promotion: googleAi,
    },
    {
      pkid: 3,
      scheduleOn: '2026-03-22',
      trainingCenterPkid: 1,
      slot: 3,
      promotionPkid: 10,
      topic: skillTrainAi.topic,
      description: skillTrainAi.description,
      trainingCenter: taipei,
      promotion: skillTrainAi,
    },
  ];

  /** Reaches protected members without loosening the component's own API. */
  const api = () => component as unknown as Record<string, any>;

  beforeEach(async () => {
    sessionStorage.clear();

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemList);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // The 異動紀錄 badge on the inline editor fetches its own trail; see the badge's own spec.
    httpMock.match((req) => req.url.endsWith('/rowaudit')).forEach((req) => req.flush([]));
    httpMock.verify();
    sessionStorage.clear();
  });

  /** Pins the grid to the 2026-03-16 week on 台北 so the specs are independent of today's date. */
  function seedState(trainingCenterPkid = 1, weekOf = '2026-03-16'): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify({ trainingCenterPkid, weekOf }));
  }

  /** Runs ngOnInit and satisfies the centre lookup plus the first query. */
  function initAndFlush(payload: FeaturedPromoItem[] = items, lookup = centres): void {
    fixture.detectChanges();
    httpMock.expectOne(centresUrl).flush(lookup);
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  function flushQuery(payload: FeaturedPromoItem[] = items): void {
    httpMock.expectOne(queryUrl).flush(payload);
    fixture.detectChanges();
  }

  // ---------- Init ----------

  it('loads the centres, then queries the restored centre and week', () => {
    seedState(2, '2026-03-18');

    fixture.detectChanges();
    httpMock.expectOne(centresUrl).flush(centres);

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ trainingCenterPkid: 2, weekOf: '2026-03-16' });
    req.flush([]);

    expect(api()['activeTrainingCenterPkid']()).toBe(2);
    expect(api()['weekLabel']()).toBe('3/16 – 3/22');
  });

  it('defaults to the default centre and the current week when nothing is stored', () => {
    fixture.detectChanges();
    httpMock.expectOne(centresUrl).flush([hsinchu, taipei]);

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({
      trainingCenterPkid: 1,
      weekOf: toIso(startOfWeek(new Date())),
    });
    req.flush([]);
  });

  it('falls back to the first centre when a stored centre no longer exists', () => {
    seedState(99);

    fixture.detectChanges();
    httpMock.expectOne(centresUrl).flush([hsinchu, taipei]);

    // hsinchu is first but taipei carries isDefault, which wins.
    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.trainingCenterPkid).toBe(1);
    req.flush([]);
  });

  it('renders one tab per centre and seven days of three slots', () => {
    seedState();
    initAndFlush();

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('p-tab')).map((t) =>
      (t as HTMLElement).textContent!.trim(),
    );
    expect(tabs).toEqual(['台北', '新竹']);

    const dayHeaders = Array.from(fixture.nativeElement.querySelectorAll('.day-header')).map((h) =>
      (h as HTMLElement).textContent!.trim(),
    );
    expect(dayHeaders).toEqual([
      '3/16 (一)',
      '3/17 (二)',
      '3/18 (三)',
      '3/19 (四)',
      '3/20 (五)',
      '3/21 (六)',
      '3/22 (日)',
    ]);
    expect(fixture.nativeElement.querySelectorAll('tr.slot-row').length).toBe(21);
  });

  it('drops each item into its (day, slot) cell and leaves the rest empty', () => {
    seedState();
    initAndFlush();

    const days = api()['days']();
    expect(days[0].slots[0].item?.pkid).toBe(1);
    expect(days[0].slots[1].item?.pkid).toBe(2);
    expect(days[0].slots[2].item).toBeNull();
    expect(days[6].slots[2].item?.pkid).toBe(3);
    expect(days.flatMap((d: any) => d.slots).filter((s: any) => s.item).length).toBe(3);

    const firstRow = fixture.nativeElement.querySelector('tr.slot-row');
    expect(firstRow.textContent).toContain('20251204_SkillTrainAI');
    expect(firstRow.textContent).toContain('成為能AI協作的程式設計師');
    expect(firstRow.classList).not.toContain('empty');
    expect(fixture.nativeElement.querySelectorAll('tr.slot-row.empty').length).toBe(18);
  });

  it('empties the grid and reports when the query fails', () => {
    seedState();
    fixture.detectChanges();
    httpMock.expectOne(centresUrl).flush(centres);
    httpMock.expectOne(queryUrl).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(api()['items']()).toEqual([]);
    expect(api()['loading']()).toBeFalse();
  });

  // ---------- Tabs and week navigation ----------

  it('switching tab re-queries that centre and persists it', () => {
    seedState();
    initAndFlush();

    api()['selectTrainingCenter'](2);

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body).toEqual({ trainingCenterPkid: 2, weekOf: '2026-03-16' });
    req.flush([]);
    expect(JSON.parse(sessionStorage.getItem(FILTERS_KEY)!).trainingCenterPkid).toBe(2);
  });

  it('ignores a tab change to the centre already shown', () => {
    seedState();
    initAndFlush();

    api()['selectTrainingCenter'](1);
    api()['selectTrainingCenter'](undefined);

    httpMock.expectNone(queryUrl);
  });

  it('steps a whole week back and forward, always landing on a Monday', () => {
    seedState();
    initAndFlush();

    api()['previousWeek']();
    const back = httpMock.expectOne(queryUrl);
    expect(back.request.body.weekOf).toBe('2026-03-09');
    back.flush([]);
    expect(api()['weekLabel']()).toBe('3/9 – 3/15');

    api()['nextWeek']();
    api()['nextWeek']();
    httpMock.match(queryUrl).forEach((r) => r.flush([]));
    expect(api()['weekLabel']()).toBe('3/23 – 3/29');
    expect(JSON.parse(sessionStorage.getItem(FILTERS_KEY)!).weekOf).toBe('2026-03-23');
  });

  it('本週 jumps back to the current week', () => {
    seedState(1, '2020-01-06');
    initAndFlush([]);

    api()['thisWeek']();

    const req = httpMock.expectOne(queryUrl);
    expect(req.request.body.weekOf).toBe(toIso(startOfWeek(new Date())));
    req.flush([]);
  });

  // ---------- Inline editor ----------

  it('Edit on a filled slot opens the editor on that item', () => {
    seedState();
    initAndFlush();
    const day = api()['days']()[0];

    api()['openEditor'](day, day.slots[1]);
    fixture.detectChanges();

    expect(api()['editor']()).toEqual({
      item: items[1],
      draft: { scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 2 },
    });
    expect(api()['isEditing'](day, day.slots[1])).toBeTrue();
    expect(api()['isEditing'](day, day.slots[0])).toBeFalse();
    expect(fixture.nativeElement.querySelectorAll('app-featured-promo-item-form').length).toBe(1);
  });

  it('Edit on an empty slot opens a blank editor at that position', () => {
    seedState();
    initAndFlush();
    const day = api()['days']()[2];

    api()['openEditor'](day, day.slots[0]);

    expect(api()['editor']()).toEqual({
      item: null,
      draft: { scheduleOn: '2026-03-18', trainingCenterPkid: 1, slot: 1 },
    });
  });

  it('closes the editor and reloads once the form reports a save', () => {
    seedState();
    initAndFlush();
    const day = api()['days']()[2];
    api()['openEditor'](day, day.slots[0]);

    api()['onSaved']({ ...items[0], pkid: 9, scheduleOn: '2026-03-18' });

    expect(api()['editor']()).toBeNull();
    flushQuery([...items, { ...items[0], pkid: 9, scheduleOn: '2026-03-18' }]);
    expect(api()['days']()[2].slots[0].item?.pkid).toBe(9);
  });

  it('closes the editor on cancel without reloading', () => {
    seedState();
    initAndFlush();
    const day = api()['days']()[0];
    api()['openEditor'](day, day.slots[0]);

    api()['closeEditor']();

    expect(api()['editor']()).toBeNull();
    httpMock.expectNone(queryUrl);
  });

  it('closes the editor when the tab or week changes', () => {
    seedState();
    initAndFlush();
    const day = api()['days']()[0];
    api()['openEditor'](day, day.slots[0]);

    api()['nextWeek']();
    httpMock.expectOne(queryUrl).flush([]);

    expect(api()['editor']()).toBeNull();
  });

  // ---------- Copy / Paste ----------

  it('Copy captures the row and Paste opens the editor pre-filled on an empty slot', () => {
    seedState();
    initAndFlush();
    expect(api()['clipboard']()).toBeNull();

    api()['copy'](items[1]);
    fixture.detectChanges();

    expect(api()['clipboard']()).toEqual({
      promotionPkid: 11,
      promoCode: '251211_GoogleAI',
      topic: 'Google AI工具一次掌握',
      description: '不需技術基礎',
    });
    // Paste buttons appear on the 18 empty slots only.
    expect(fixture.nativeElement.querySelectorAll('p-button[ariaLabel="貼上"]').length).toBe(18);

    const day = api()['days']()[3];
    api()['paste'](day, day.slots[1]);

    expect(api()['editor']()).toEqual({
      item: null,
      draft: {
        scheduleOn: '2026-03-19',
        trainingCenterPkid: 1,
        slot: 2,
        promotionPkid: 11,
        promoCode: '251211_GoogleAI',
        topic: 'Google AI工具一次掌握',
        description: '不需技術基礎',
      },
    });
  });

  it('Paste does nothing on a filled slot or with an empty clipboard', () => {
    seedState();
    initAndFlush();
    const day = api()['days']()[0];

    api()['paste'](day, day.slots[2]);
    expect(api()['editor']()).toBeNull();

    api()['copy'](items[0]);
    api()['paste'](day, day.slots[0]);
    expect(api()['editor']()).toBeNull();
  });

  // ---------- Slot moves ----------

  it('+ moves the item down and - moves it up, then reloads', () => {
    seedState();
    initAndFlush();

    api()['moveDown'](items[0]);
    let req = httpMock.expectOne(`${baseUrl}/1/move-down`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...items[0], slot: 2 });
    flushQuery();

    api()['moveUp'](items[1]);
    req = httpMock.expectOne(`${baseUrl}/2/move-up`);
    expect(req.request.method).toBe('POST');
    req.flush({ ...items[1], slot: 1 });
    flushQuery();
  });

  it('disables - on slot 1, + on slot 3, and both on an empty slot', () => {
    seedState();
    initAndFlush();
    const days = api()['days']();

    expect(api()['canMoveUp'](days[0].slots[0])).toBeFalse();
    expect(api()['canMoveDown'](days[0].slots[0])).toBeTrue();
    expect(api()['canMoveUp'](days[0].slots[1])).toBeTrue();
    expect(api()['canMoveDown'](days[6].slots[2])).toBeFalse();
    expect(api()['canMoveUp'](days[0].slots[2])).toBeFalse();
    expect(api()['canMoveDown'](days[0].slots[2])).toBeFalse();

    const firstRowButtons = fixture.nativeElement
      .querySelector('tr.slot-row')
      .querySelectorAll('button');
    expect(firstRowButtons[0].disabled).toBeFalse(); // +
    expect(firstRowButtons[1].disabled).toBeTrue(); // -
  });

  it('keeps the grid when a move fails', () => {
    seedState();
    initAndFlush();

    api()['moveDown'](items[0]);
    httpMock
      .expectOne(`${baseUrl}/1/move-down`)
      .flush('boom', { status: 500, statusText: 'Server Error' });

    httpMock.expectNone(queryUrl);
    expect(api()['items']().length).toBe(3);
  });

  // ---------- Delete ----------

  it('names the day, slot, and escaped code in the confirmation message', () => {
    seedState();
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    const confirm = spyOn(confirmationService, 'confirm').and.returnValue(confirmationService);
    const day = api()['days']()[0];

    api()['confirmDelete'](day, {
      ...items[0],
      promotion: { ...skillTrainAi, promoCode: '<img src=x>' },
    });

    const message = confirm.calls.mostRecent().args[0].message as string;
    expect(message).toContain('3/16 (一)');
    expect(message).toContain('版位 <b>1</b>');
    expect(message).toContain('&lt;img src=x&gt;');
    expect(message).not.toContain('<img');
  });

  it('deletes the item and reloads once the confirmation is accepted', () => {
    seedState();
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](api()['days']()[0], items[0]);

    const deleteReq = httpMock.expectOne(`${baseUrl}/1`);
    expect(deleteReq.request.method).toBe('DELETE');
    deleteReq.flush(null);

    flushQuery(items.slice(1));
    expect(api()['items']().length).toBe(2);
  });

  it('keeps the item when the delete fails', () => {
    seedState();
    initAndFlush();
    const confirmationService = fixture.debugElement.injector.get(ConfirmationService);
    spyOn(confirmationService, 'confirm').and.callFake((options: any) => {
      options.accept();
      return confirmationService;
    });

    api()['confirmDelete'](api()['days']()[0], items[0]);
    httpMock.expectOne(`${baseUrl}/1`).flush('boom', { status: 500, statusText: 'Server Error' });

    httpMock.expectNone(queryUrl);
    expect(api()['items']().length).toBe(3);
  });
});
