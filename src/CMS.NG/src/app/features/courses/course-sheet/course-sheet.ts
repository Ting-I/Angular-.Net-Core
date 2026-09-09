import { Component, computed, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';

import { Course } from '@core/models/course.model';

/**
 * Who the sheet says it is from. One object rather than literals in the template (eng review E6),
 * and the 業務洽詢 line renders **only when the phone and email are real** (E8): a placeholder phone
 * number on a document a customer files for months is worse than no contact line at all. Until
 * marketing supplies them (Open Question 3), the QR and the printed URL are the buyer's route back.
 *
 * E6 put this next to `PUBLIC_COURSE_URL` in `course-detail.ts`; it lives here instead because the
 * sheet is its only consumer and this keeps the parent's input list to the record's own data.
 */
export const SHEET_CONTACT = {
  companyName: '恆逸教育訓練中心',
  phone: '',
  email: '',
};

/** Placeholder for a fact the record does not carry. Never applied to a long-text section — an empty section is omitted whole (D7). */
const EMPTY = '—';

/**
 * One long-text section of the sheet: a heading and its body, in the order a buyer reads them.
 *
 * `html` says which of the two shapes this column actually holds. The CMS's long-text columns carry
 * **operator-pasted HTML** for most courses — headings, `<ul>` outlines, `<font color>` vendor notes,
 * and often malformed nesting — but plain text for others, and the two cannot be rendered the same
 * way: interpolating HTML prints tags as characters on a customer document, and `innerHTML` on plain
 * text collapses the operator's line breaks, which *are* the outline's structure.
 */
interface SheetSection {
  label: string;
  text: string;
  html: boolean;
}

/**
 * Does this value carry markup? A tag name from the set operators actually paste is enough — the test
 * only has to be right about *which renderer to use*, and both renderers are safe either way.
 */
const MARKUP = /<\/?(p|br|div|span|ul|ol|li|h[1-6]|strong|b|em|i|u|font|table|tr|td|a|img)\b[^>]*>/i;

/**
 * The one tag that is content even though it strips to no text. A section whose column holds a pasted
 * diagram and nothing else must still print, heading and all — see `normalise`.
 */
const IMAGE = /<img\b/i;

/** Tags stripped, entities that render as blank removed — what is left is what a buyer would read. */
function textOf(html: string): string {
  return html
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;|&#160;|&#xa0;/gi, ' ')
    .trim();
}

/**
 * 課程簡介 Course sheet — the print-only, customer-facing view of one course.
 *
 * **The browser is the PDF engine.** There is no PDF library here and none on the API host: the
 * operator's own Chrome renders this component to A4 through `window.print()`, which is the only
 * approach that emits real, selectable, searchable vector Traditional Chinese text without shipping
 * a CJK font or a headless Chromium anywhere (jsPDF cannot render CJK from `.html()`; html2canvas
 * rasterises). The parent (`CourseDetail`) owns the print lifecycle; this component is a leaf with
 * inputs only, so it can be lifted onto a `/courses/:id/sheet` route or a list-page loop unchanged.
 *
 * **Every field on it is buyer-facing, and the excluded ones are excluded by construction.** No
 * `pkid` (it appears only as a path segment inside the footer URL), no 顯示順序, 上架狀態, 課程群組,
 * 友善網址, 對應職務類別, 備註, 其他資訊 or 使用狀況 counts — and **no price in any form**: no
 * `listPrice`, no 牌價 cell, no 以報價單為準 disclaimer (Final Gate decision C4). A `DEFAULT 0`
 * column cannot tell "free" from "not filled", and a frozen, dated price on a document that sits
 * beside a purchase order for months is a quote nobody agreed to give. `course-sheet.spec.ts`
 * asserts the absence of each of them; the price assertion is the strongest test in the suite,
 * because the value is right there in the model this component receives.
 *
 * **Print margins come from the page box**, `@page sheet { margin: 14mm 16mm }` in `styles.scss`
 * (page rules cannot live in a component stylesheet). That is rung 3 of the eng review's E3 ladder,
 * and it is where QA landed rather than where the plan started. Rung 1 was `margin: 0` — the only
 * lever that stops Chrome stamping the internal CMS URL into a customer document's margin — with the
 * sheet supplying its own margins: host padding for left/right, and a repeated `<thead>` for the top
 * of each page. Printing a real two-page course on 2026-09-09 showed Chrome does not repeat an empty
 * header row: page 2 opened flush against the paper edge with its first line clipped. Block padding
 * cannot rescue that either (`box-decoration-break: clone` is unimplemented for blocks), so the
 * margin moved to the one thing that applies per page by definition, and the frame table went away.
 *
 * The cost is that the stamp is reachable again when the operator leaves the print dialog's
 * "Headers and footers" ticked; the button's tooltip on the detail page says to untick it, and
 * Chrome remembers that per user. A clipped page 2 could not be turned off at all.
 *
 * The footer is static rather than a repeated `<tfoot>`: `tfoot` repetition is unreliable, and where
 * it does repeat it sits directly after the content on the final fragment, which would ride halfway
 * up a one-page sheet.
 */
@Component({
  selector: 'app-course-sheet',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './course-sheet.html',
  styleUrl: './course-sheet.scss',
})
export class CourseSheet {
  /** The record, already loaded. The sheet is never rendered without one. */
  readonly course = input.required<Course>();

  /**
   * 對應認證 — resolved names only. The parent filters out lookup misses, so the `認證 #{pkid}`
   * fallback the screen shows can never reach a customer.
   */
  readonly certificationNames = input<string[]>([]);

  /**
   * The QR as a PNG data URL — an `<img>`, never a CSS background, so it prints whatever the
   * dialog's "Background graphics" setting says. Null in practice never happens (the parent's
   * generator returns a data URL even when the canvas fails); kept nullable so the sheet can be
   * reused without a QR, which the spec guards.
   */
  readonly qrImage = input<string | null>(null);

  /** The public course URL, printed verbatim as the footer link. Never recomputed here. */
  readonly qrUrl = input.required<string>();

  /** 產生日期 — set by the parent on `beforeprint`, so a sheet left open overnight prints today. */
  readonly generatedOn = input.required<Date>();

  protected readonly contact = SHEET_CONTACT;
  protected readonly empty = EMPTY;

  /** True only when both contact values are real; a half-filled block does not print (E8). */
  protected readonly hasContact = computed(
    () => SHEET_CONTACT.phone.trim().length > 0 && SHEET_CONTACT.email.trim().length > 0,
  );

  /** 官方課程名稱 — omitted entirely when blank, so `''` from the form does not print a blank line. */
  protected readonly officialTitle = computed(() => this.course().officialTitle?.trim() || null);

  /** 課程代碼 — its own line disappears when the record carries no code (D17). */
  protected readonly courseId = computed(() => this.course().courseId.trim() || null);

  protected readonly prodCourseId = computed(() => this.course().prodCourseId.trim() || null);

  /** 對應認證, comma-joined. The facts grid keeps the row and shows — when there are none. */
  protected readonly certifications = computed(() => this.certificationNames().join('、'));

  /**
   * The long-text sections in buyer order (D6): why they would take it, then who it is for, then
   * what it covers, then the exam, then what they get handed. **An empty section is omitted heading
   * and all** — a customer document with six 「—」 headings reads as an unfinished form.
   */
  protected readonly sections = computed<SheetSection[]>(() => {
    const course = this.course();
    return (
      [
        { label: '課程目標', text: course.objective },
        { label: '適合對象', text: course.target },
        { label: '先備知識', text: course.prerequisites },
        { label: '課程大綱', text: course.outline },
        { label: '考試／認證說明', text: course.towardCertOrExam },
        { label: '教材', text: course.material },
      ] satisfies { label: string; text: string | null }[]
    )
      .map((section) => this.normalise(section.label, section.text))
      .filter((section): section is SheetSection => section !== null);
  });

  /**
   * Operator-entered copy arrives in whichever of the two shapes the operator pasted, and empty is a
   * third shape that must not print a heading.
   *
   * Plain text: runs of blank lines collapse to one so a page break is not spent on emptiness (D15),
   * and the rest is rendered `pre-wrap` — those line breaks are the outline.
   *
   * HTML: left intact for the template to bind through `[innerHTML]`, which Angular sanitizes (script,
   * event handlers and `style` are dropped, and the parser repairs the malformed nesting that
   * hand-pasted vendor copy is full of). Emptiness is judged on the *text* inside it, so a column
   * holding `<p>&nbsp;</p>` — which reads as blank on paper — omits its section like any other blank.
   * An image is the exception, and it has to be: a 課程大綱 pasted as one diagram strips to no text at
   * all, and dropping it would take the heading with it and tell nobody. `.sheet-section-html img` is
   * already styled for exactly this, so the renderer was always expecting it.
   */
  private normalise(label: string, value: string | null): SheetSection | null {
    const text = value?.replace(/\r\n/g, '\n').trim();
    if (!text) {
      return null;
    }

    if (MARKUP.test(text)) {
      return textOf(text) || IMAGE.test(text) ? { label, text, html: true } : null;
    }

    return { label, text: text.replace(/\n{3,}/g, '\n\n'), html: false };
  }
}
