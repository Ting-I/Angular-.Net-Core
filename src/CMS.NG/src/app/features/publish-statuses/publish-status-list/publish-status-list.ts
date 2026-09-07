import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';

import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusQuery } from '@core/models/publish-status.model';

const FILTERS_KEY = 'publish-status-list-filters';
const SORT_KEY = 'publish-status-list-sort';
const PAGE_KEY = 'publish-status-list-page';

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

const EMPTY_FILTERS: PublishStatusQuery = {
  keyword: null,
  isDraft: null,
  isPublished: null,
  isDiscontinued: null,
};

@Component({
  selector: 'app-publish-status-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    ToastModule,
    ConfirmDialogModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './publish-status-list.html',
  styleUrl: './publish-status-list.scss',
})
export class PublishStatusList implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly statuses = signal<PublishStatus[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Tri-state options for the three bit columns: null keeps the filter off. */
  protected readonly triStateOptions = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  /** Draft filter values bound to the drawer; applied only on 搜尋. */
  protected draftFilters: PublishStatusQuery = { ...EMPTY_FILTERS };
  protected appliedFilters: PublishStatusQuery = { ...EMPTY_FILTERS };

  protected sort: ListSort = { field: 'pkid', order: 1 };
  protected page: ListPage = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  /** Number of applied filters — shown as the 搜尋條件 button badge. */
  protected get activeFilterCount(): number {
    const { keyword, isDraft, isPublished, isDiscontinued } = this.appliedFilters;
    return (
      (keyword?.trim() ? 1 : 0) +
      [isDraft, isPublished, isDiscontinued].filter((value) => value !== null && value !== undefined)
        .length
    );
  }

  protected get hasActiveFilters(): boolean {
    return this.activeFilterCount > 0;
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: (statuses) => {
        this.statuses.set(statuses);
        this.loading.set(false);
      },
      error: () => {
        this.statuses.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得發布狀態清單。',
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
    void this.router.navigate(['/publish-statuses/new']);
  }

  protected view(status: PublishStatus): void {
    void this.router.navigate(['/publish-statuses', status.pkid]);
  }

  protected edit(status: PublishStatus): void {
    void this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
  }

  protected confirmDelete(status: PublishStatus): void {
    const inUse = status.courseCount + status.promotionCount > 0;
    const warning = inUse
      ? `<br>此狀態仍被 ${status.courseCount} 筆課程與 ${status.promotionCount} 筆活動使用，將無法刪除。`
      : '';

    // p-confirmDialog renders the message with [innerHTML], so the record's own text is escaped.
    this.confirmationService.confirm({
      header: '刪除發布狀態',
      message: `確定要刪除主代碼 <b>${status.pkid}</b>「${escapeHtml(status.description)}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(status),
    });
  }

  private delete(status: PublishStatus): void {
    this.service.delete(status.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: status.description,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail:
            error.status === 409
              ? '此發布狀態仍被課程或活動使用，無法刪除。'
              : '請稍後再試。',
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
