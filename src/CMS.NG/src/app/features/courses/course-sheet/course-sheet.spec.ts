import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Course } from '@core/models/course.model';
import { CourseSheet } from './course-sheet';

/**
 * The sheet leaves the building: it is the one view in this CMS a customer reads. So most of this
 * spec is negative — it proves that the fields the operator sees on the admin page are *not* on the
 * printed one, with a fixture whose excluded values are all distinctive enough that a leak is
 * unmistakable. The strongest case is the price: `listPrice: 24000` is deliberately present in the
 * model handed to the component (Final Gate C4 removed every price binding, so the value must reach
 * the template through nothing at all). Do not "tidy" it out of the fixture — that would disarm the
 * assertion, not simplify it.
 */
describe('CourseSheet', () => {
  let fixture: ComponentFixture<CourseSheet>;

  const qrImage = 'data:image/png;base64,AAAA';
  const qrUrl = 'https://www.uuu.com.tw/Course/Show/987654/AZ-104';
  const generatedOn = new Date(2026, 8, 9);

  const course: Course = {
    pkid: 987654,
    title: 'Azure 系統管理',
    officialTitle: 'Microsoft Azure Administrator',
    courseId: 'AZ-104',
    prodCourseId: 'P-AZ-104',
    friendlyUrl: 'friendly-url-must-not-print',
    displayOrder: 8642,
    partnerPkid: 1,
    courseGroupPkid: 2,
    publishStatusPkid: 3,
    scheduleOn: '2026-03-04',
    scheduleOff: '2036-05-06',
    hour: 21,
    listPrice: 24000,
    learningCredit: 3.5,
    material: '官方教材與實作手冊',
    objective: '學會管理 Azure 訂閱',
    target: '系統管理人員',
    prerequisites: '基本網路知識',
    outline: '第一章\n第二章',
    towardCertOrExam: 'AZ-104 認證考試',
    note: '內部備註不可外流',
    otherInfo: '其他資訊不可外流',
    canRepeat: true,
    partner: { pkid: 1, name: 'Microsoft', appKey: 'MS' },
    courseGroup: { pkid: 2, description: '雲端技術群組' },
    publishStatus: { pkid: 3, description: '已上架狀態' },
    courseFaqCount: 4,
    courseRelatedLinkCount: 3,
    hotCourseCount: 2,
    courseRecommCount: 1,
    certificationPkids: [7],
    jobCategoryPkids: [3],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CourseSheet] }).compileComponents();
  });

  /** Renders one sheet. Called twice in a spec that compares two records; each call is a fresh component. */
  async function render(
    overrides: Partial<Course> = {},
    inputs: { certificationNames?: string[]; qrImage?: string | null } = {},
  ): Promise<void> {
    fixture = TestBed.createComponent(CourseSheet);
    fixture.componentRef.setInput('course', { ...course, ...overrides });
    fixture.componentRef.setInput('certificationNames', inputs.certificationNames ?? [
      'Azure Administrator',
    ]);
    fixture.componentRef.setInput(
      'qrImage',
      inputs.qrImage === undefined ? qrImage : inputs.qrImage,
    );
    fixture.componentRef.setInput('qrUrl', qrUrl);
    fixture.componentRef.setInput('generatedOn', generatedOn);
    fixture.detectChanges();
  }

  const sheet = (): HTMLElement => fixture.nativeElement.querySelector('article.sheet');

  /** Everything above the footer. The footer legitimately carries the public URL, pkid and all. */
  const bodyText = (): string =>
    fixture.nativeElement.querySelector('.sheet-body').textContent as string;

  const allText = (): string => fixture.nativeElement.textContent as string;

  const sectionTitles = (): string[] =>
    Array.from(
      fixture.nativeElement.querySelectorAll('.sheet-section-title') as NodeListOf<HTMLElement>,
    ).map((el) => el.textContent!.trim());

  const fact = (name: string): string | null => {
    const el = fixture.nativeElement.querySelector(`.fact-${name} dd`) as HTMLElement | null;
    return el ? el.textContent!.trim() : null;
  };

  describe('what a buyer reads', () => {
    beforeEach(async () => {
      await render();
    });

    it('prints the course identity, the facts and every long-text section', () => {
      const text = bodyText();

      expect(text).toContain('課程簡介');
      expect(text).toContain('恆逸教育訓練中心');
      expect(text).toContain('Azure 系統管理');
      expect(text).toContain('Microsoft Azure Administrator');
      expect(text).toContain('AZ-104');
      expect(text).toContain('P-AZ-104');
      expect(text).toContain('Microsoft');
      expect(text).toContain('官方教材與實作手冊');
      expect(text).toContain('Azure Administrator');
      expect(sectionTitles()).toEqual([
        '課程目標',
        '適合對象',
        '先備知識',
        '課程大綱',
        '考試／認證說明',
        '教材',
      ]);
    });

    it('prints the facts with their units and the repeat flag as 是／否', () => {
      expect(fact('code')).toBe('AZ-104');
      expect(fact('partner')).toBe('Microsoft');
      expect(fact('hour')).toBe('21 小時');
      expect(fact('credit')).toBe('3.5');
      expect(fact('prod-code')).toBe('P-AZ-104');
      expect(fact('repeat')).toBe('是');
      expect(fact('certs')).toBe('Azure Administrator');
    });

    it('carries the cost line, the generation date and the public URL as a real link', () => {
      const footer = fixture.nativeElement.querySelector('.sheet-footer') as HTMLElement;

      expect(footer.textContent).toContain('費用請洽業務');
      expect(footer.textContent).toContain('產生日期 2026/09/09');

      const link = footer.querySelector('a.sheet-footer-url') as HTMLAnchorElement;
      expect(link.textContent!.trim()).toBe(qrUrl);
      expect(link.getAttribute('href')).toBe(qrUrl);
    });

    it('renders the QR as an <img> carrying the data URL it was given', () => {
      const image = sheet().querySelector('img.sheet-qr-image') as HTMLImageElement;

      expect(image.getAttribute('src')).toBe(qrImage);
      expect(image.getAttribute('alt')).toBe('QR Code：掃描前往課程網頁');
    });

    it('declares the document language, so the PDF text layer picks CJK glyphs', () => {
      expect(sheet().getAttribute('lang')).toBe('zh-Hant');
      expect(sheet().querySelector('.sheet-official-title')!.getAttribute('lang')).toBe('en');
    });

    it('is invisible on screen — it exists only under print media', () => {
      expect(getComputedStyle(fixture.nativeElement).display).toBe('none');
    });

    it('renders none of the admin page own link class', () => {
      expect(sheet().querySelectorAll('a.detail-link').length).toBe(0);
    });
  });

  describe('what must never print', () => {
    beforeEach(async () => {
      await render();
    });

    it('carries no price under any input — no value, no label, no disclaimer', () => {
      expect(allText()).not.toContain('24,000');
      expect(allText()).not.toContain('24000');
      expect(allText()).not.toContain('牌價');
      expect(allText()).not.toContain('定價');
      expect(allText()).not.toContain('以報價單為準');
    });

    it('carries no price when the column holds 0 either', async () => {
      await render({ listPrice: 0 });

      expect(allText()).not.toContain('0 元');
      expect(allText()).not.toContain('牌價');
      expect(allText()).not.toContain('洽詢');
    });

    it('never prints the pkid outside the public URL', () => {
      expect(bodyText()).not.toContain('987654');
      expect(fixture.nativeElement.querySelector('.sheet-footer')!.textContent).toContain('987654');
    });

    it('omits every internal field', () => {
      const text = allText();

      expect(text).not.toContain('friendly-url-must-not-print');
      expect(text).not.toContain('內部備註不可外流');
      expect(text).not.toContain('其他資訊不可外流');
      expect(text).not.toContain('2026-03-04');
      expect(text).not.toContain('2036-05-06');
      expect(text).not.toContain('2026/03/04');
      expect(text).not.toContain('雲端技術群組');
      expect(text).not.toContain('已上架狀態');
      expect(text).not.toContain('8642');
    });

    it('omits every internal label', () => {
      const text = allText();

      for (const label of [
        '主代碼',
        '顯示順序',
        '友善網址',
        '上架狀態',
        '課程群組',
        '上架日期',
        '下架日期',
        '備註',
        '其他資訊',
        '課程問答數',
        '相關連結數',
        '熱門課程數',
        '推薦課程數',
        '職務',
      ]) {
        expect(text).withContext(label).not.toContain(label);
      }
    });
  });

  describe('records that are missing something', () => {
    it('omits the 官方課程名稱 element entirely when it is null or blank', async () => {
      await render({ officialTitle: null });
      expect(sheet().querySelector('.sheet-official-title')).toBeNull();

      await render({ officialTitle: '  ' });
      expect(sheet().querySelector('.sheet-official-title')).toBeNull();
    });

    it('hides the 課程代碼 line when the record carries no code', async () => {
      await render({ courseId: '   ' });

      expect(sheet().querySelector('.fact-code')).toBeNull();
      expect(fact('partner')).toBe('Microsoft');
    });

    it('shows — for a null 原廠, a zero 時數 and a zero 點數', async () => {
      await render({ partner: null, hour: 0, learningCredit: 0 });

      expect(fact('partner')).toBe('—');
      expect(fact('hour')).toBe('—');
      expect(fact('credit')).toBe('—');
    });

    it('shows — for 對應認證 when nothing resolved, keeping the row', async () => {
      await render({}, { certificationNames: [] });

      expect(fact('certs')).toBe('—');
    });

    it('prints 否 for a course that cannot be repeated', async () => {
      await render({ canRepeat: false });

      expect(fact('repeat')).toBe('否');
    });

    it('omits an empty long-text section, heading and all', async () => {
      await render({
        objective: null,
        target: '   ',
        prerequisites: null,
        outline: '第一章',
        towardCertOrExam: null,
        material: null,
      });

      expect(sectionTitles()).toEqual(['課程大綱']);
    });

    it('renders no <img> when no QR was supplied', async () => {
      await render({}, { qrImage: null });

      expect(sheet().querySelector('img.sheet-qr-image')).toBeNull();
      expect(sheet().querySelector('.sheet-qr')).not.toBeNull();
    });

    it('collapses runs of blank lines in operator-entered text', async () => {
      await render({
        objective: null,
        target: null,
        prerequisites: null,
        towardCertOrExam: null,
        material: null,
        outline: '第一章\n\n\n\n第二章',
      });

      expect(
        (fixture.nativeElement.querySelector('.sheet-section-text') as HTMLElement).textContent,
      ).toBe('第一章\n\n第二章');
    });

    it('joins several certifications into one line', async () => {
      await render({}, { certificationNames: ['Azure Administrator', 'Azure Solutions Architect'] });

      expect(fact('certs')).toBe('Azure Administrator、Azure Solutions Architect');
    });
  });

  /**
   * The CMS's long-text columns hold operator-pasted HTML for most courses. Interpolating it would
   * print `<p>` and `<li>` as characters on a document a buyer files beside a purchase order, so those
   * sections are bound through `[innerHTML]` and Angular's default sanitizer. The fixture below is a
   * real 課程大綱 paste, malformed exactly as it arrived: a stray `</ul>`, `<li>` never closed, and a
   * `<font color>` vendor note.
   */
  describe('long text that is really HTML', () => {
    const outlineHtml = [
      '<p>【網路基礎】</p>',
      '<ul style="list-style:disc">',
      '<li>網路基礎架構與網路服務</li>',
      '</ul>',
      '<p>【CompTIA Security＋國際網路資安認證】</p>',
      '<ul style="list-style:disc">',
      '<li>CompTIA Security＋國際網路資安認證課程<font color="#BD0000">(此課程為「數位發展部資通安全署」認可之資通安全專業證照)</font><br>',
      '</ul>',
      '</ul>【藍隊資安防禦通識-EC-Council CND認證】</p>',
      '<ul style="list-style:disc">',
      '<li>藍隊資安防禦通識-EC-Council CND認證考試輔導</li>',
      '</ul>',
    ].join('\n');

    const outlineSection = (): HTMLElement =>
      fixture.nativeElement.querySelector('.sheet-section-html');

    it('renders the markup as real elements, not as visible tags', async () => {
      await render({ outline: outlineHtml });

      const section = outlineSection();
      expect(section).not.toBeNull();
      expect(section.querySelectorAll('li').length).toBe(3);
      expect(section.querySelectorAll('ul').length).toBeGreaterThan(0);

      const text = section.textContent as string;
      expect(text).toContain('網路基礎架構與網路服務');
      expect(text).toContain('CompTIA Security＋國際網路資安認證課程');
      expect(text).toContain('認可之資通安全專業證照');
      expect(text).not.toContain('<li>');
      expect(text).not.toContain('<p>');
      expect(text).not.toContain('list-style');
    });

    it('drops script, event handlers and inline styles on the way in', async () => {
      await render({
        outline:
          '<p onclick="steal()">【網路基礎】</p><script>steal()</script>' +
          '<ul style="list-style:disc"><li><img src="x" onerror="steal()">網路服務</li></ul>',
      });

      const section = outlineSection();
      expect(section.querySelector('script')).toBeNull();
      expect(section.innerHTML).not.toContain('onclick');
      expect(section.innerHTML).not.toContain('onerror');
      expect(section.innerHTML).not.toContain('steal()');
      // The copy itself survives: sanitizing is not censoring.
      expect(section.textContent).toContain('【網路基礎】');
      expect(section.textContent).toContain('網路服務');
    });

    it('keeps plain-text columns on the plain-text path, line breaks and all', async () => {
      await render({
        objective: null,
        target: null,
        prerequisites: null,
        towardCertOrExam: null,
        material: null,
        outline: '第一章\n第二章',
      });

      expect(fixture.nativeElement.querySelector('.sheet-section-html')).toBeNull();
      expect(
        (fixture.nativeElement.querySelector('p.sheet-section-text') as HTMLElement).textContent,
      ).toBe('第一章\n第二章');
    });

    it('omits a section whose HTML carries no readable text', async () => {
      await render({
        objective: '<p>&nbsp;</p>',
        target: '<p><br></p>',
        prerequisites: null,
        outline: outlineHtml,
        towardCertOrExam: null,
        material: null,
      });

      expect(sectionTitles()).toEqual(['課程大綱']);
    });

    it('still prints no internal field when the copy is HTML', async () => {
      await render({ outline: outlineHtml });

      expect(allText()).not.toContain('24,000');
      expect(allText()).not.toContain('friendly-url-must-not-print');
      expect(bodyText()).not.toContain('987654');
    });
  });

});
