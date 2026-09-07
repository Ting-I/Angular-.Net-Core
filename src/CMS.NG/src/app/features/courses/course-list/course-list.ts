import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseQuery } from '@core/models/course.model';
import { PartnerLookup } from '@core/models/partner-lookup.model';
import { CourseGroupLookup } from '@core/models/course-group-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';
import { fromIso, toIso } from '@core/utils/date.util';

const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

interface ListSort {
  field: string;
  order: number;
}

interface ListPage {
  first: number;
  rows: number;
}

/** The drawer binds Date objects; the query carries ISO strings. */
interface DraftFilters {
  keyword: string | null;
  partnerPkid: number | null;
  courseGroupPkid: number | null;
  publishStatusPkid: number | null;
  scheduleOnFrom: Date | null;
  scheduleOnTo: Date | null;
  scheduleOffFrom: Date | null;
  scheduleOffTo: Date | null;
  canRepeat: boolean | null;
}

/** The confirm dialog renders its message as HTML, so record text must be escaped first. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

const EMPTY_FILTERS: CourseQuery = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null,
  canRepeat: null,
};

@Component({
  selector: 'app-course-list',
  imports: [
    FormsModule,
    DatePipe,
    DecimalPipe,
    TableModule,
    ButtonModule,
    DrawerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    DatePickerModule,
    TagModule,
    TooltipModule,
    ToastModule,
    ConfirmDialogModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss',
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Drawer select options. The nav objects supply the table labels; these are filters only. */
  protected readonly partners = signal<PartnerLookup[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatusLookup[]>([]);

  /** Tri-state options for the repeat filter: null keeps the filter off. */
  protected readonly canRepeatOptions = [
    { label: '全部', value: null },
    { label: '允許', value: true },
    { label: '不允許', value: false },
  ];

  protected draftFilters: DraftFilters = this.toDraft(EMPTY_FILTERS);
  protected appliedFilters: CourseQuery = { ...EMPTY_FILTERS };

  protected sort: ListSort = { field: 'displayOrder', order: 1 };
  protected page: ListPage = { first: 0, rows: 20 };

  // --- copy dialog ---
  protected readonly copyDialogVisible = signal(false);
  protected readonly copying = signal(false);
  protected readonly copySource = signal<Course | null>(null);
  protected readonly copyError = signal<string | null>(null);
  protected copyCourseId = '';

  ngOnInit(): void {
    this.restoreState();
    this.applyRouteParams();

    // The lookups only populate the drawer, but the restored filters need their labels, so the
    // first query waits for them. Each is caught on its own: forkJoin cancels its siblings on the
    // first error, which would drop two working dropdowns because the third failed.
    let lookupFailed = false;
    const recover = <T>(source: Observable<T[]>) =>
      source.pipe(
        catchError(() => {
          lookupFailed = true;
          return of([] as T[]);
        }),
      );

    forkJoin({
      partners: recover(this.lookupService.getPartners()),
      courseGroups: recover(this.lookupService.getCourseGroups()),
      publishStatuses: recover(this.lookupService.getPublishStatuses()),
    }).subscribe(({ partners, courseGroups, publishStatuses }) => {
      this.partners.set(partners);
      this.courseGroups.set(courseGroups);
      this.publishStatuses.set(publishStatuses);

      if (lookupFailed) {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得篩選選項。',
        });
      }

      this.load();
    });
  }

  protected get partnerOptions(): { pkid: number; label: string }[] {
    return this.partners().map((p) => ({ pkid: p.pkid, label: `${p.name}（${p.appKey}）` }));
  }

  protected get courseGroupOptions(): { pkid: number; label: string }[] {
    return this.courseGroups().map((g) => ({ pkid: g.pkid, label: g.description }));
  }

  protected get publishStatusOptions(): { pkid: number; label: string }[] {
    return this.publishStatuses().map((s) => ({ pkid: s.pkid, label: s.description }));
  }

  /** Number of applied filters — shown as the 搜尋條件 button badge. */
  protected get activeFilterCount(): number {
    const f = this.appliedFilters;
    const set = (value: unknown): number => (value !== null && value !== undefined ? 1 : 0);

    return (
      (f.keyword?.trim() ? 1 : 0) +
      set(f.partnerPkid) +
      set(f.courseGroupPkid) +
      set(f.publishStatusPkid) +
      set(f.scheduleOnFrom) +
      set(f.scheduleOnTo) +
      set(f.scheduleOffFrom) +
      set(f.scheduleOffTo) +
      // canRepeat === false (不允許) is a real filter; a falsy check would silently drop it.
      set(f.canRepeat)
    );
  }

  protected get hasActiveFilters(): boolean {
    return this.activeFilterCount > 0;
  }

  /** Total rows in the four child tables — drives the delete warning. */
  protected referenceCount(course: Course): number {
    return (
      course.courseFaqCount +
      course.courseRelatedLinkCount +
      course.hotCourseCount +
      course.courseRecommCount
    );
  }

  protected referenceBreakdown(course: Course): string {
    return (
      `課程問答 ${course.courseFaqCount}、相關連結 ${course.courseRelatedLinkCount}、` +
      `熱門課程 ${course.hotCourseCount}、推薦課程 ${course.courseRecommCount}`
    );
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: (courses) => {
        this.courses.set(courses);
        this.loading.set(false);
      },
      error: () => {
        this.courses.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得課程清單。',
        });
      },
    });
  }

  protected openFilterDrawer(): void {
    this.draftFilters = this.toDraft(this.appliedFilters);
    this.filterDrawerVisible.set(true);
  }

  protected applyFilters(): void {
    this.appliedFilters = this.fromDraft(this.draftFilters);
    this.page = { ...this.page, first: 0 };
    this.persistState();
    this.filterDrawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.draftFilters = this.toDraft(EMPTY_FILTERS);
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
    void this.router.navigate(['/courses/new']);
  }

  protected view(course: Course): void {
    void this.router.navigate(['/courses', course.pkid]);
  }

  protected edit(course: Course): void {
    void this.router.navigate(['/courses', course.pkid, 'edit']);
  }

  // --- copy ---

  protected openCopyDialog(course: Course): void {
    this.copySource.set(course);
    this.copyCourseId = '';
    this.copyError.set(null);
    this.copyDialogVisible.set(true);
  }

  protected confirmCopy(): void {
    const source = this.copySource();
    const newCourseId = this.copyCourseId.trim();
    if (!source) {
      return;
    }

    if (!newCourseId) {
      this.copyError.set('簡介代碼為必填。');
      return;
    }

    this.copying.set(true);
    this.copyError.set(null);
    this.service.copy(source.pkid, { newCourseId }).subscribe({
      next: (created) => {
        this.copying.set(false);
        this.copyDialogVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '已複製',
          detail: `${created.courseId} ${created.title}`,
        });
        void this.router.navigate(['/courses', created.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.copying.set(false);
        // 409 keeps the dialog open so the operator can pick another code.
        this.copyError.set(error.status === 409 ? '簡介代碼已存在。' : '複製失敗，請稍後再試。');
      },
    });
  }

  protected cancelCopy(): void {
    this.copyDialogVisible.set(false);
  }

  protected confirmDelete(course: Course): void {
    const warning =
      this.referenceCount(course) > 0
        ? `<br>此課程仍被 ${course.courseFaqCount} 筆課程問答、${course.courseRelatedLinkCount} 筆相關連結、` +
          `${course.hotCourseCount} 筆熱門課程與 ${course.courseRecommCount} 筆推薦課程使用，將無法刪除。`
        : '';

    // p-confirmDialog renders the message with [innerHTML], so the record text is escaped.
    this.confirmationService.confirm({
      header: '刪除課程',
      message:
        `確定要刪除主代碼 <b>${course.pkid}</b>` +
        `「${escapeHtml(course.courseId)} ${escapeHtml(course.title)}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(course),
    });
  }

  private delete(course: Course): void {
    this.service.delete(course.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: course.title,
        });
        this.load();
      },
      error: (error: { status?: number }) =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: error.status === 409 ? '此課程仍被其他資料使用，無法刪除。' : '請稍後再試。',
        }),
    });
  }

  /**
   * Cross-entity navigation from Partner / CourseGroup / PublishStatus. An incoming param replaces
   * that one saved filter and leaves the rest of the restored state alone.
   */
  private applyRouteParams(): void {
    const params = this.route.snapshot.queryParamMap;
    let changed = false;

    for (const key of ['partnerPkid', 'courseGroupPkid', 'publishStatusPkid'] as const) {
      const raw = params.get(key);
      if (raw === null) {
        continue;
      }

      const value = Number(raw);
      if (Number.isInteger(value)) {
        this.appliedFilters = { ...this.appliedFilters, [key]: value };
        changed = true;
      }
    }

    if (changed) {
      this.draftFilters = this.toDraft(this.appliedFilters);
      this.page = { ...this.page, first: 0 };
      this.persistState();
    }
  }

  private toDraft(filters: CourseQuery): DraftFilters {
    return {
      keyword: filters.keyword ?? null,
      partnerPkid: filters.partnerPkid ?? null,
      courseGroupPkid: filters.courseGroupPkid ?? null,
      publishStatusPkid: filters.publishStatusPkid ?? null,
      scheduleOnFrom: fromIso(filters.scheduleOnFrom),
      scheduleOnTo: fromIso(filters.scheduleOnTo),
      scheduleOffFrom: fromIso(filters.scheduleOffFrom),
      scheduleOffTo: fromIso(filters.scheduleOffTo),
      canRepeat: filters.canRepeat ?? null,
    };
  }

  private fromDraft(draft: DraftFilters): CourseQuery {
    return {
      keyword: draft.keyword,
      partnerPkid: draft.partnerPkid,
      courseGroupPkid: draft.courseGroupPkid,
      publishStatusPkid: draft.publishStatusPkid,
      scheduleOnFrom: toIso(draft.scheduleOnFrom),
      scheduleOnTo: toIso(draft.scheduleOnTo),
      scheduleOffFrom: toIso(draft.scheduleOffFrom),
      scheduleOffTo: toIso(draft.scheduleOffTo),
      canRepeat: draft.canRepeat,
    };
  }

  private persistState(): void {
    this.writeSession(FILTERS_KEY, this.appliedFilters);
    this.writeSession(SORT_KEY, this.sort);
    this.writeSession(PAGE_KEY, this.page);
  }

  private restoreState(): void {
    this.appliedFilters = this.readSession(FILTERS_KEY, this.appliedFilters);
    this.draftFilters = this.toDraft(this.appliedFilters);
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
