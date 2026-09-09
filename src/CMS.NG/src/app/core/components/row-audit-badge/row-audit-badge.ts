import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';
import { formatDateTime } from '@core/utils/date.util';

/** Shown on the badge and in the dialog when the record has no trail yet. */
export const NO_HISTORY_TEXT = '尚無異動紀錄 No history';

/** Shown when the fetch failed — not the same thing as having no history, and it must not read as it. */
export const HISTORY_UNAVAILABLE_TEXT = '異動紀錄無法載入 Unavailable';

/**
 * 異動紀錄 History — one record's audit trail, as a badge that opens a dialog.
 *
 * Lives in `core/components` rather than under a feature because every detail and form page in the
 * app renders one; it belongs to no single entity. It is the only component outside `features/`,
 * and `core/` is where the other cross-cutting pieces already are.
 *
 * **It takes the database table name and the record's pkid, not the route.** `tableName` is the
 * real table — `Course`, `FeaturedPromoItem` — because that is what the writer stored, and `pkid`
 * is the surrogate key even for a record the operator knows by a string: 角色 AppRole and
 * 使用者 AppUser are keyed on RoleId and UserId, but `AuditHelper.PrimaryKeyValue` records the
 * pkid every table here also carries. Passing the string key would quietly show an empty trail.
 *
 * **The latest entry is on the badge itself.** A history nobody opens is a history nobody reads,
 * so the most recent change is rendered inline — the dialog is for the rest of it.
 *
 * **The fetch follows the input.** A detail page knows its pkid only once the record has loaded,
 * and a form page in 新增 mode never has one, so the load is an `effect` over the inputs rather
 * than an `ngOnInit`: a pkid that arrives late still fetches, and one that never arrives never
 * does.
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [ButtonModule, DialogModule],
  templateUrl: './row-audit-badge.html',
  styleUrl: './row-audit-badge.scss',
})
export class RowAuditBadge {
  private readonly service = inject(RowAuditService);

  /** 資料表名稱 — the database table name the audit rows were written against. */
  readonly tableName = input.required<string>();

  /** 主代碼 — the record's pkid; null while it is unknown, or on a record that does not exist yet. */
  readonly pkid = input<number | null>(null);

  /** Bound by the template, which cannot reach a module constant on its own. */
  protected readonly noHistoryText = NO_HISTORY_TEXT;
  protected readonly unavailableText = HISTORY_UNAVAILABLE_TEXT;

  protected readonly entries = signal<RowAuditEntry[]>([]);
  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly dialogVisible = signal(false);

  /** The API answers newest first, so the head of the list is the most recent change. */
  protected readonly latest = computed<RowAuditEntry | null>(() => this.entries()[0] ?? null);

  /** What the badge says beside its label: the latest change, or why there is nothing to show. */
  protected readonly latestText = computed(() => {
    if (this.loading()) {
      return '載入中…';
    }

    if (this.failed()) {
      return HISTORY_UNAVAILABLE_TEXT;
    }

    const latest = this.latest();
    if (latest === null) {
      return NO_HISTORY_TEXT;
    }

    const when = formatDateTime(latest.dateTime);
    const who = `${latest.actionType} by ${latest.userName}`;
    return when === null ? who : `${who} · ${when}`;
  });

  /**
   * Guards against an out-of-order answer: an input that changes twice in quick succession leaves
   * two requests on the wire, and the one that arrives last is not necessarily the one asked for
   * last.
   */
  private sequence = 0;

  constructor() {
    effect(() => this.load(this.tableName(), this.pkid()));
  }

  /** `yyyy-MM-dd HH:mm`, or the raw value if it is not a timestamp this can read. */
  protected when(entry: RowAuditEntry): string {
    return formatDateTime(entry.dateTime) ?? entry.dateTime;
  }

  protected open(): void {
    this.dialogVisible.set(true);
  }

  private load(tableName: string, pkid: number | null): void {
    const request = ++this.sequence;

    // No key means no record yet — nothing to ask about, and no request to make.
    if (pkid === null || pkid === undefined) {
      this.entries.set([]);
      this.loading.set(false);
      this.failed.set(false);
      return;
    }

    this.loading.set(true);
    this.failed.set(false);

    this.service
      .getForRecord(tableName, pkid)
      .pipe(catchError(() => of(null)))
      .subscribe((entries) => {
        if (request !== this.sequence) {
          return;
        }

        this.entries.set(entries ?? []);
        this.failed.set(entries === null);
        this.loading.set(false);
      });
  }
}
