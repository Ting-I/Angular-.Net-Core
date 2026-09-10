import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EMPTY_TEXT, LongText } from './long-text';

/**
 * The screen renderer for the CMS's long-text columns. `richText` decides *what* the value is and is
 * pinned by its own spec; this pins what the operator ends up looking at — real elements rather than
 * visible tags, line breaks kept where they are the structure, and a 「—」 where the column is blank.
 */
describe('LongText', () => {
  let fixture: ComponentFixture<LongText>;

  async function render(value: string | null): Promise<void> {
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    await fixture.whenStable();
  }

  const html = (): HTMLElement => fixture.nativeElement.querySelector('.long-text-html');
  const plain = (): HTMLElement => fixture.nativeElement.querySelector('.long-text-plain');

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [LongText] }).compileComponents();
    fixture = TestBed.createComponent(LongText);
  });

  it('renders 「—」 for null, empty, and markup that reads as blank', async () => {
    for (const blank of [null, '', '   ', '<p>&nbsp;</p>']) {
      await render(blank);

      expect(fixture.nativeElement.textContent.trim()).toBe(EMPTY_TEXT);
      expect(html()).toBeNull();
      expect(plain()).toBeNull();
    }
  });

  it('renders plain text on the plain path, so its line breaks survive', async () => {
    await render('第一章\n第二章');

    expect(html()).toBeNull();
    expect(plain().textContent).toBe('第一章\n第二章');
    // pre-wrap is what makes those breaks visible; without it the column reads as one line.
    expect(getComputedStyle(plain()).whiteSpace).toBe('pre-wrap');
  });

  /**
   * The defect this component exists for. A real 課程大綱 paste, malformed exactly as it arrived: a
   * stray `</ul>`, an unclosed `<li>`, and a `<font color>` vendor note. Interpolated, the operator
   * saw the tags themselves.
   */
  it('renders operator-pasted markup as real elements, not as visible tags', async () => {
    await render(
      [
        '<p>【網路基礎】</p>',
        '<ul style="list-style:disc">',
        '<li>網路基礎架構與網路服務</li>',
        '<li>CompTIA Security＋認證<font color="#BD0000">(資通安全專業證照)</font><br>',
        '</ul>',
        '</ul>【藍隊資安防禦通識】</p>',
      ].join('\n'),
    );

    const section = html();
    expect(section).not.toBeNull();
    expect(section.querySelectorAll('li').length).toBe(2);
    expect(section.querySelectorAll('ul').length).toBeGreaterThan(0);

    const text = section.textContent as string;
    expect(text).toContain('網路基礎架構與網路服務');
    expect(text).toContain('資通安全專業證照');
    expect(text).not.toContain('<li>');
    expect(text).not.toContain('<p>');
    expect(text).not.toContain('list-style');
  });

  it('drops script, event handlers and inline styles on the way in', async () => {
    await render(
      '<p onclick="steal()">【網路基礎】</p><script>steal()</script>' +
        '<ul style="list-style:disc"><li><img src="x" onerror="steal()">網路服務</li></ul>',
    );

    const section = html();
    expect(section.querySelector('script')).toBeNull();
    expect(section.innerHTML).not.toContain('onclick');
    expect(section.innerHTML).not.toContain('onerror');
    expect(section.innerHTML).not.toContain('steal()');
    // The copy itself survives: sanitizing is not censoring.
    expect(section.textContent).toContain('【網路基礎】');
    expect(section.textContent).toContain('網路服務');
  });

  it('keeps a column whose only content is a pasted diagram', async () => {
    await render('<p><img src="/media/outline.png"></p>');

    expect(html()).not.toBeNull();
    expect(html().querySelector('img')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain(EMPTY_TEXT);
  });

  it('re-renders when the record it is showing changes', async () => {
    await render('第一章');
    expect(plain()).not.toBeNull();

    await render('<p>第一章</p>');
    expect(plain()).toBeNull();
    expect(html()).not.toBeNull();
  });
});
