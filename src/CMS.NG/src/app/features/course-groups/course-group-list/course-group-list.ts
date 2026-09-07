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

import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupQuery } from '@core/models/course-group.model';

const FILTERS_KEY = 'course-group-list-filters';
const SORT_KEY = 'course-group-list-sort';
const PAGE_KEY = 'course-group-list-page';

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

const EMPTY_FILTERS: CourseGroupQuery = {
  keyword: null,
  inUse: null,
};

@Component({
  selector: 'app-course-group-list',
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
  templateUrl: './course-group-list.html',
  styleUrl: './course-group-list.scss',
})
export class CourseGroupList implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly courseGroups = signal<CourseGroup[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Tri-state options for the usage filter: null keeps the filter off. */
  protected readonly inUseOptions = [
    { label: '全部', value: null },
    { label: '已被引用', value: true },
    { label: '未被引用', value: false },
  ];

  /** Draft filter values bound to the drawer; applied only on 搜尋. */
  protected draftFilters: CourseGroupQuery = { ...EMPTY_FILTERS };
  protected appliedFilters: CourseGroupQuery = { ...EMPTY_FILTERS };

  protected sort: ListSort = { field: 'pkid', order: 1 };
  protected page: ListPage = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  /** Number of applied filters — shown as the 搜尋條件 button badge. */
  protected get activeFilterCount(): number {
    const { keyword, inUse } = this.appliedFilters;
    // inUse === false (未被引用) is a real filter; a falsy check would silently drop it.
    return (keyword?.trim() ? 1 : 0) + (inUse !== null && inUse !== undefined ? 1 : 0);
  }

  protected get hasActiveFilters(): boolean {
    return this.activeFilterCount > 0;
  }

  /** Total across the two referencing tables — drives the delete warning. */
  protected referenceCount(courseGroup: CourseGroup): number {
    return courseGroup.courseCount + courseGroup.partnerCourseGroupCount;
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: (courseGroups) => {
        this.courseGroups.set(courseGroups);
        this.loading.set(false);
      },
      error: () => {
        this.courseGroups.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得課程群組清單。',
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
    void this.router.navigate(['/course-groups/new']);
  }

  protected view(courseGroup: CourseGroup): void {
    void this.router.navigate(['/course-groups', courseGroup.pkid]);
  }

  protected edit(courseGroup: CourseGroup): void {
    void this.router.navigate(['/course-groups', courseGroup.pkid, 'edit']);
  }

  protected confirmDelete(courseGroup: CourseGroup): void {
    const warning =
      this.referenceCount(courseGroup) > 0
        ? `<br>此群組仍被 ${courseGroup.courseCount} 筆課程與 ` +
          `${courseGroup.partnerCourseGroupCount} 筆原廠課程群組使用，將無法刪除。`
        : '';

    // p-confirmDialog renders the message with [innerHTML], so the record's own text is escaped.
    this.confirmationService.confirm({
      header: '刪除課程群組',
      message:
        `確定要刪除主代碼 <b>${courseGroup.pkid}</b>` +
        `「${escapeHtml(courseGroup.description)}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(courseGroup),
    });
  }

  private delete(courseGroup: CourseGroup): void {
    this.service.delete(courseGroup.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: courseGroup.description,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: error.status === 409 ? '此課程群組仍被其他資料使用，無法刪除。' : '請稍後再試。',
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
