import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';

import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerQuery } from '@core/models/partner.model';

const FILTERS_KEY = 'partner-list-filters';
const SORT_KEY = 'partner-list-sort';
const PAGE_KEY = 'partner-list-page';

interface ListSort {
  field: string;
  order: number;
}

interface ListPage {
  first: number;
  rows: number;
}

/** The confirm dialog renders its message as HTML, so record text must be escaped first. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

const EMPTY_FILTERS: PartnerQuery = {
  keyword: null,
  hasImage: null,
};

@Component({
  selector: 'app-partner-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TooltipModule,
    ToastModule,
    ConfirmDialogModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './partner-list.html',
})
export class PartnerList implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Tri-state options for the image filter: null keeps the filter off. */
  protected readonly hasImageOptions = [
    { label: '全部', value: null },
    { label: '有圖', value: true },
    { label: '無圖', value: false },
  ];

  /** Draft filter values bound to the drawer; applied only on 搜尋. */
  protected draftFilters: PartnerQuery = { ...EMPTY_FILTERS };
  protected appliedFilters: PartnerQuery = { ...EMPTY_FILTERS };

  protected sort: ListSort = { field: 'displayOrder', order: 1 };
  protected page: ListPage = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  /** Number of applied filters — shown as the 搜尋條件 button badge. */
  protected get activeFilterCount(): number {
    const { keyword, hasImage } = this.appliedFilters;
    // hasImage === false (無圖) is a real filter; a falsy check would silently drop it.
    return (keyword?.trim() ? 1 : 0) + (hasImage !== null && hasImage !== undefined ? 1 : 0);
  }

  protected get hasActiveFilters(): boolean {
    return this.activeFilterCount > 0;
  }

  /**
   * Total number of rows referencing this partner. The five counts are shown separately on the
   * detail page; the list would be unreadable with five extra columns.
   */
  protected referenceCount(partner: Partner): number {
    return (
      partner.certificationCount +
      partner.courseCount +
      partner.courseGroupCount +
      partner.promotionCount +
      partner.seminarCount
    );
  }

  protected referenceBreakdown(partner: Partner): string {
    return (
      `課程 ${partner.courseCount}、認證 ${partner.certificationCount}、` +
      `課程群組 ${partner.courseGroupCount}、活動 ${partner.promotionCount}、` +
      `說明會 ${partner.seminarCount}`
    );
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: (partners) => {
        this.partners.set(partners);
        this.loading.set(false);
      },
      error: () => {
        this.partners.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得原廠清單。',
        });
      },
    });
  }

  protected openFilterDrawer(): void {
    this.draftFilters = { ...this.appliedFilters };
    this.filterDrawerVisible.set(true);
  }

  protected applyFilters(): void {
    this.appliedFilters = { ...this.draftFilters };
    this.page = { ...this.page, first: 0 };
    this.persistState();
    this.filterDrawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.draftFilters = { ...EMPTY_FILTERS };
    this.applyFilters();
  }

  protected onSort(event: { field?: string | string[] | null; order?: number | null }): void {
    const field = Array.isArray(event.field) ? event.field[0] : event.field;
    if (!field) {
      return;
    }
    this.sort = { field, order: event.order ?? 1 };
    this.persistState();
  }

  protected onPage(event: { first?: number; rows?: number }): void {
    this.page = { first: event.first ?? 0, rows: event.rows ?? 20 };
    this.persistState();
  }

  protected add(): void {
    void this.router.navigate(['/partners/new']);
  }

  protected view(partner: Partner): void {
    void this.router.navigate(['/partners', partner.pkid]);
  }

  protected edit(partner: Partner): void {
    void this.router.navigate(['/partners', partner.pkid, 'edit']);
  }

  protected confirmDelete(partner: Partner): void {
    const warning =
      this.referenceCount(partner) > 0
        ? `<br>此原廠仍被 ${partner.courseCount} 筆課程、${partner.certificationCount} 筆認證、` +
          `${partner.courseGroupCount} 筆課程群組、${partner.promotionCount} 筆活動與 ` +
          `${partner.seminarCount} 筆說明會使用，將無法刪除。`
        : '';

    // p-confirmDialog renders the message with [innerHTML], so the record's own text is escaped.
    this.confirmationService.confirm({
      header: '刪除原廠',
      message: `確定要刪除主代碼 <b>${partner.pkid}</b>「${escapeHtml(partner.name)}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(partner),
    });
  }

  private delete(partner: Partner): void {
    this.service.delete(partner.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: partner.name,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: error.status === 409 ? '此原廠仍被其他資料使用，無法刪除。' : '請稍後再試。',
        }),
    });
  }

  private persistState(): void {
    this.writeSession(FILTERS_KEY, this.appliedFilters);
    this.writeSession(SORT_KEY, this.sort);
    this.writeSession(PAGE_KEY, this.page);
  }

  private restoreState(): void {
    this.appliedFilters = this.readSession(FILTERS_KEY, this.appliedFilters);
    this.draftFilters = { ...this.appliedFilters };
    this.sort = this.readSession(SORT_KEY, this.sort);
    this.page = this.readSession(PAGE_KEY, this.page);
  }

  private writeSession(key: string, value: unknown): void {
    try {
      sessionStorage.setItem(key, JSON.stringify(value));
    } catch {
      // Session storage can be unavailable (private mode); state is a convenience only.
    }
  }

  private readSession<T>(key: string, fallback: T): T {
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? (JSON.parse(raw) as T) : fallback;
    } catch {
      return fallback;
    }
  }
}
