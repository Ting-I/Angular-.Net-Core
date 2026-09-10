import { Component, computed, input } from '@angular/core';

import { richText } from '@core/utils/rich-text.util';

/** Shown when the column is blank, so the field keeps its place in the detail grid. */
export const EMPTY_TEXT = '—';

/**
 * One operator-entered long-text column, rendered on screen the way it was written.
 *
 * **Why it exists.** The CMS's long-text columns hold operator-pasted HTML for most rows (see
 * `richText`). The course detail page interpolated all eight of them, so a real 課程大綱 rendered as
 * a wall of visible `<p>` and `<li>` tags — the same defect the 課程簡介 sheet fixed for the printed
 * PDF, still on the screen the operator actually checks the record on.
 *
 * **Why a component rather than eight `@if` blocks.** Every long-text field needs the same
 * three-way branch (markup / plain text / blank), and inlining it eight times would be seventy lines
 * of duplicated template. It also puts the `::ng-deep` rules for the rendered subtree behind one
 * wrapper class instead of loose in a page stylesheet: nodes inserted by `[innerHTML]` never carry a
 * component's scoping attribute, so those rules cannot be scoped the ordinary way and the smallest
 * host that can bound them is the right place for them.
 *
 * It lives in `core/components` beside `RowAuditBadge` for the same reason that one does: it belongs
 * to no single entity. Course is its first consumer, not its only possible one.
 *
 * **The sheet does not use it.** `CourseSheet` prints black-on-white in pt and mm and omits an empty
 * section heading and all; this renders screen typography and keeps the field with a 「—」. They
 * share `richText`, which is the part that has to agree.
 */
@Component({
  selector: 'app-long-text',
  templateUrl: './long-text.html',
  styleUrl: './long-text.scss',
})
export class LongText {
  /** The raw column value, straight off the record. Null and blank both render as 「—」. */
  readonly value = input<string | null>(null);

  /** Bound by the template, which cannot reach a module constant on its own. */
  protected readonly emptyText = EMPTY_TEXT;

  protected readonly body = computed(() => richText(this.value()));
}
