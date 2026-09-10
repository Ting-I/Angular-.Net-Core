/**
 * Operator-pasted long text — which of the two shapes is it, and is it blank?
 *
 * The CMS's long-text columns (`Course.Outline`, `Material`, …) hold **HTML** for most rows:
 * `<p>` headings, `<ul style="list-style:disc">` outlines, `<font color="#BD0000">` vendor notes,
 * and frequently malformed nesting (a stray `</ul>`, an unclosed `<li>`). They hold **plain text**
 * for the rest. The two cannot be rendered the same way — interpolating HTML prints the tags as
 * visible characters, and `innerHTML` on plain text collapses the operator's line breaks, which
 * *are* the outline's structure — so every view of these columns has to make the same call.
 *
 * It lives here, not in a component, because two views already need it and they disagree about
 * everything else: `CourseSheet` prints to paper and omits an empty section heading and all, while
 * `LongText` renders to screen and keeps the field with a 「—」. What they share is exactly this
 * classification, so this is the piece that is shared.
 */

/**
 * Does this value carry markup? A tag name from the set operators actually paste is enough — the
 * test only has to be right about *which renderer to use*, and both renderers are safe either way.
 */
const MARKUP = /<\/?(p|br|div|span|ul|ol|li|h[1-6]|strong|b|em|i|u|font|table|tr|td|a|img)\b[^>]*>/i;

/**
 * The one tag that is content even though it strips to no text. A column holding a pasted diagram
 * and nothing else must still render: dropping it would take the field's heading with it and tell
 * nobody. `img` is styled on both the sheet and the screen, so the renderers were always expecting
 * it.
 */
const IMAGE = /<img\b/i;

/** Tags stripped, entities that render as blank removed — what is left is what a reader would read. */
export function textOf(html: string): string {
  return html
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;|&#160;|&#xa0;/gi, ' ')
    .trim();
}

/** One long-text value, classified. */
export interface RichText {
  /** The value to render, `\r\n` normalised and trimmed. Never empty. */
  text: string;
  /** True when `text` must be bound through `[innerHTML]`; false when it is plain text. */
  html: boolean;
}

/**
 * Classifies one long-text column value, or returns `null` when it reads as blank.
 *
 * Emptiness is judged on the *text inside* the markup, so a column holding `<p>&nbsp;</p>` — which
 * reads as blank to a human — is blank here too, rather than rendering an empty paragraph or, worse,
 * printing the tags. The `<img>` exception above is the one case where markup with no text is still
 * content.
 *
 * Callers bind `text` through `[innerHTML]` when `html` is true, **relying on Angular's default
 * sanitizer** — never `bypassSecurityTrust`, which would put unreviewed operator copy straight into
 * the DOM. The sanitizer drops script, event handlers and inline styles, and the parser repairs the
 * broken nesting that hand-pasted vendor copy is full of.
 */
export function richText(value: string | null | undefined): RichText | null {
  const text = value?.replace(/\r\n/g, '\n').trim();
  if (!text) {
    return null;
  }

  if (MARKUP.test(text)) {
    return textOf(text) || IMAGE.test(text) ? { text, html: true } : null;
  }

  return { text, html: false };
}
