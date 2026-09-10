import { richText, textOf } from './rich-text.util';

/**
 * The classifier both views of the CMS's long-text columns depend on: `CourseSheet` for the printed
 * 課程簡介 and `LongText` for the admin screen. Getting it wrong is visible either way — tags printed
 * as characters on a customer document, or an operator's line breaks collapsed into one paragraph —
 * so the shapes it has to tell apart are pinned here rather than through either renderer.
 *
 * The HTML fixtures are real 課程大綱 pastes, malformed exactly as they arrived.
 */
describe('rich-text.util', () => {
  describe('textOf', () => {
    it('strips tags and leaves the readable copy', () => {
      expect(textOf('<p>【網路基礎】</p><ul><li>網路服務</li></ul>')).toBe('【網路基礎】網路服務');
    });

    it('treats the blank entities as blank, not as text', () => {
      expect(textOf('<p>&nbsp;</p>')).toBe('');
      expect(textOf('<p>&#160;&#xA0;</p>')).toBe('');
    });
  });

  describe('richText', () => {
    it('reports null, undefined, empty and whitespace-only as blank', () => {
      expect(richText(null)).toBeNull();
      expect(richText(undefined)).toBeNull();
      expect(richText('')).toBeNull();
      expect(richText('   \n\t  ')).toBeNull();
    });

    it('classifies plain text as plain, trimmed, line breaks intact', () => {
      expect(richText('  第一章\n第二章  ')).toEqual({ text: '第一章\n第二章', html: false });
    });

    it('normalises CRLF so a Windows paste is not two characters per break', () => {
      expect(richText('第一章\r\n第二章')).toEqual({ text: '第一章\n第二章', html: false });
    });

    it('classifies operator-pasted markup as HTML and hands it back untouched', () => {
      const outline = [
        '<p>【網路基礎】</p>',
        '<ul style="list-style:disc">',
        '<li>網路基礎架構與網路服務</li>',
        '</ul>',
        '</ul>【藍隊資安防禦通識】</p>',
      ].join('\n');

      // Sanitizing is the renderer's job, through Angular's default sanitizer. This only decides
      // *which* renderer, so the malformed nesting must survive to the parser that repairs it.
      expect(richText(outline)).toEqual({ text: outline, html: true });
    });

    it('recognises a bare <br> and a <font color> vendor note as markup', () => {
      expect(richText('第一行<br>第二行')?.html).toBeTrue();
      expect(richText('<font color="#BD0000">認可之資通安全專業證照</font>')?.html).toBeTrue();
    });

    /**
     * The case that reads as blank to a human and as content to a naive length check. A column
     * holding an empty paragraph must not render one, and must not print its tags either.
     */
    it('treats markup that strips to no text as blank', () => {
      expect(richText('<p>&nbsp;</p>')).toBeNull();
      expect(richText('<p></p><ul></ul>')).toBeNull();
    });

    /**
     * The exception, and it has to be one: a 課程大綱 pasted as a single diagram strips to no text at
     * all. Dropping it would take the field's own heading with it and tell nobody.
     */
    it('keeps image-only markup, which strips to no text but is still content', () => {
      const diagram = '<p><img src="/media/outline.png"></p>';

      expect(richText(diagram)).toEqual({ text: diagram, html: true });
    });

    it('does not mistake a stray angle bracket in prose for markup', () => {
      expect(richText('適合 5 < 10 的情境')?.html).toBeFalse();
      expect(richText('見 <備註> 一節')?.html).toBeFalse();
    });
  });
});
