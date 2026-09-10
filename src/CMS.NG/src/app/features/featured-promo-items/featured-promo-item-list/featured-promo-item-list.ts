import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';

import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  MAX_SLOT,
  MIN_SLOT,
  SLOTS,
} from '@core/models/featured-promo-item.model';
import { TrainingCenterLookup } from '@core/models/training-center-lookup.model';
import { addDays, fromIso, startOfWeek, toIso } from '@core/utils/date.util';
import {
  FeaturedPromoItemDraft,
  FeaturedPromoItemForm,
} from '../featured-promo-item-form/featured-promo-item-form';

/**
 * Only one key: the page has no sortable columns or paginator, so the `-sort` / `-page` keys the
 * other lists persist have nothing to hold here.
 */
const FILTERS_KEY = 'featured-promo-item-list-filters';

interface ListFilters {
  trainingCenterPkid: number | null;
  /** Monday of the displayed week, yyyy-MM-dd. */
  weekOf: string | null;
}

/** One of the three slots on a day; `item` is null while the slot is empty. */
export interface DaySlot {
  slot: number;
  item: FeaturedPromoItem | null;
}

export interface DayRow {
  date: Date;
  iso: string;
  label: string;
  slots: DaySlot[];
}

/** What Copy captures — everything a new item needs except its position. */
interface Clipboard {
  promotionPkid: number;
  promoCode: string;
  topic: string;
  description: string;
}

interface EditorState {
  item: FeaturedPromoItem | null;
  draft: FeaturedPromoItemDraft;
}

const WEEKDAY_LABELS = ['一', '二', '三', '四', '五', '六', '日'];

/** The confirm dialog renders its message as HTML, so record text must be escaped first. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function shortDate(date: Date): string {
  return `${date.getMonth() + 1}/${date.getDate()}`;
}

/**
 * Week grid: one tab per training centre, one Monday..Sunday week at a time, three slots per
 * day. Editing happens inline in the grid rather than on a separate route — see the feature spec.
 */
@Component({
  selector: 'app-featured-promo-item-list',
  imports: [ButtonModule, TabsModule, ToastModule, ConfirmDialogModule, FeaturedPromoItemForm],
  providers: [MessageService, ConfirmationService],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss',
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly trainingCenters = signal<TrainingCenterLookup[]>([]);
  protected readonly activeTrainingCenterPkid = signal<number | null>(null);
  protected readonly weekStart = signal<Date>(startOfWeek(new Date()));
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly clipboard = signal<Clipboard | null>(null);
  protected readonly editor = signal<EditorState | null>(null);

  protected readonly weekEnd = computed(() => addDays(this.weekStart(), 6));
  protected readonly weekLabel = computed(
    () => `${shortDate(this.weekStart())} – ${shortDate(this.weekEnd())}`,
  );

  /** Seven days × three slots, with the loaded items dropped into their (day, slot) cells. */
  protected readonly days = computed<DayRow[]>(() => {
    const byCell = new Map(this.items().map((item) => [`${item.scheduleOn}#${item.slot}`, item]));
    return Array.from({ length: 7 }, (_, offset) => {
      const date = addDays(this.weekStart(), offset);
      const iso = toIso(date)!;
      return {
        date,
        iso,
        label: `${shortDate(date)} (${WEEKDAY_LABELS[offset]})`,
        slots: SLOTS.map((slot) => ({ slot, item: byCell.get(`${iso}#${slot}`) ?? null })),
      };
    });
  });

  ngOnInit(): void {
    this.restoreState();
    this.lookupService.getTrainingCenters().subscribe({
      next: (centres) => {
        this.trainingCenters.set(centres);
        const active = this.activeTrainingCenterPkid();
        if (!centres.some((c) => c.pkid === active)) {
          const fallback = centres.find((c) => c.isDefault) ?? centres[0];
          this.activeTrainingCenterPkid.set(fallback?.pkid ?? null);
        }
        this.load();
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得訓練中心清單。',
        });
      },
    });
  }

  // ---------- Tabs and week navigation ----------

  /** p-tabs emits the tab value; the guard keeps a stray undefined from clearing the filter. */
  protected selectTrainingCenter(value: unknown): void {
    const pkid = Number(value);
    if (!Number.isFinite(pkid) || pkid === this.activeTrainingCenterPkid()) {
      return;
    }
    this.activeTrainingCenterPkid.set(pkid);
    this.editor.set(null);
    this.persistState();
    this.load();
  }

  protected previousWeek(): void {
    this.goToWeek(addDays(this.weekStart(), -7));
  }

  protected nextWeek(): void {
    this.goToWeek(addDays(this.weekStart(), 7));
  }

  protected thisWeek(): void {
    this.goToWeek(startOfWeek(new Date()));
  }

  private goToWeek(monday: Date): void {
    this.weekStart.set(monday);
    this.editor.set(null);
    this.persistState();
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.service
      .query({
        trainingCenterPkid: this.activeTrainingCenterPkid(),
        weekOf: toIso(this.weekStart()),
      })
      .subscribe({
        next: (items) => {
          this.items.set(items);
          this.loading.set(false);
        },
        error: () => {
          this.items.set([]);
          this.loading.set(false);
          this.messageService.add({
            severity: 'error',
            summary: '載入失敗',
            detail: '無法取得上稿資料。',
          });
        },
      });
  }

  // ---------- Inline editor ----------

  /** Edit on a filled slot edits it; Edit on an empty slot starts a new item there. */
  protected openEditor(day: DayRow, cell: DaySlot): void {
    const trainingCenterPkid = this.activeTrainingCenterPkid();
    if (trainingCenterPkid === null) {
      return;
    }
    this.editor.set({
      item: cell.item,
      draft: { scheduleOn: day.iso, trainingCenterPkid, slot: cell.slot },
    });
  }

  protected isEditing(day: DayRow, cell: DaySlot): boolean {
    const editor = this.editor();
    return (
      editor !== null && editor.draft.scheduleOn === day.iso && editor.draft.slot === cell.slot
    );
  }

  protected closeEditor(): void {
    this.editor.set(null);
  }

  protected onSaved(item: FeaturedPromoItem): void {
    this.editor.set(null);
    this.messageService.add({
      severity: 'success',
      summary: '已儲存',
      detail: `${item.promotion.promoCode} — ${item.topic}`,
    });
    this.load();
  }

  // ---------- Copy / Paste ----------

  protected copy(item: FeaturedPromoItem): void {
    this.clipboard.set({
      promotionPkid: item.promotionPkid,
      promoCode: item.promotion.promoCode,
      topic: item.topic,
      description: item.description,
    });
    this.messageService.add({
      severity: 'info',
      summary: '已複製',
      detail: `${item.promotion.promoCode}，可貼上至空白版位。`,
    });
  }

  /** Paste opens the editor on an empty slot with the copied values filled in but not yet saved. */
  protected paste(day: DayRow, cell: DaySlot): void {
    const clipboard = this.clipboard();
    const trainingCenterPkid = this.activeTrainingCenterPkid();
    if (!clipboard || cell.item || trainingCenterPkid === null) {
      return;
    }
    this.editor.set({
      item: null,
      draft: { scheduleOn: day.iso, trainingCenterPkid, slot: cell.slot, ...clipboard },
    });
  }

  // ---------- Slot moves ----------

  protected canMoveUp(cell: DaySlot): boolean {
    return cell.item !== null && cell.item.slot > MIN_SLOT;
  }

  protected canMoveDown(cell: DaySlot): boolean {
    return cell.item !== null && cell.item.slot < MAX_SLOT;
  }

  /** The "-" action: 2 → 1. */
  protected moveUp(item: FeaturedPromoItem): void {
    this.move(this.service.moveUp(item.pkid));
  }

  /** The "+" action: 1 → 2. */
  protected moveDown(item: FeaturedPromoItem): void {
    this.move(this.service.moveDown(item.pkid));
  }

  private move(request: ReturnType<FeaturedPromoItemService['moveUp']>): void {
    this.editor.set(null);
    request.subscribe({
      next: () => this.load(),
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '移動失敗',
          detail: '請稍後再試。',
        }),
    });
  }

  // ---------- Delete ----------

  protected confirmDelete(day: DayRow, item: FeaturedPromoItem): void {
    // p-confirmDialog renders the message with [innerHTML], so the record's own text is escaped.
    this.confirmationService.confirm({
      header: '刪除上稿資料',
      message:
        `確定要刪除 <b>${escapeHtml(day.label)}</b> 版位 <b>${item.slot}</b>` +
        `「${escapeHtml(item.promotion.promoCode)}」？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(item),
    });
  }

  private delete(item: FeaturedPromoItem): void {
    this.editor.set(null);
    this.service.delete(item.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: item.promotion.promoCode,
        });
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: '請稍後再試。',
        }),
    });
  }

  // ---------- Session state ----------

  private persistState(): void {
    const filters: ListFilters = {
      trainingCenterPkid: this.activeTrainingCenterPkid(),
      weekOf: toIso(this.weekStart()),
    };
    try {
      sessionStorage.setItem(FILTERS_KEY, JSON.stringify(filters));
    } catch {
      // Session storage can be unavailable (private mode); state is a convenience only.
    }
  }

  private restoreState(): void {
    let filters: ListFilters | null = null;
    try {
      const raw = sessionStorage.getItem(FILTERS_KEY);
      filters = raw ? (JSON.parse(raw) as ListFilters) : null;
    } catch {
      filters = null;
    }
    if (!filters) {
      return;
    }

    this.activeTrainingCenterPkid.set(filters.trainingCenterPkid ?? null);
    const weekOf = fromIso(filters.weekOf);
    if (weekOf) {
      this.weekStart.set(startOfWeek(weekOf));
    }
  }
}
