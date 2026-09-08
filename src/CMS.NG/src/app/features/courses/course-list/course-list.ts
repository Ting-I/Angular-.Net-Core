import { Component, ElementRef, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';
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

/**
 * Columns the list edits in place.
 *
 * `pkid`, `partner.name` and `courseGroup.description` are deliberately absent: the key is
 * immutable on edit, and the two FK labels are lookups the Edit form owns. Anything not keyed here
 * is read-only — `startEdit` refuses a field it cannot find, so the guard is not the template's
 * alone.
 */
type EditableField =
  | 'displayOrder'
  | 'courseId'
  | 'prodCourseId'
  | 'title'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'canRepeat';

type EditorKind = 'text' | 'number' | 'date' | 'select' | 'checkbox';

/** What a cell editor hands back before it is written into a CourseRequest. */
type EditValue = string | number | Date | boolean | null;

interface EditableColumn {
  kind: EditorKind;
  /** Used verbatim in the inline validation messages. */
  label: string;
  maxLength?: number;
  /** Decimal places the column stores; 0 means the value must be a whole number. */
  decimals?: number;
  /** Upper bound implied by the SQL type — smallint / decimal(9, n). */
  max?: number;
}

const EDITABLE_COLUMNS: Record<EditableField, EditableColumn> = {
  displayOrder: { kind: 'number', label: '顯示順序', decimals: 0 },
  courseId: { kind: 'text', label: '簡介代碼', maxLength: 50 },
  prodCourseId: { kind: 'text', label: '科目代碼', maxLength: 50 },
  title: { kind: 'text', label: '課程名稱', maxLength: 200 },
  publishStatusPkid: { kind: 'select', label: '上架狀態' },
  scheduleOn: { kind: 'date', label: '上架日期' },
  scheduleOff: { kind: 'date', label: '下架日期' },
  // Hour is smallint; ListPrice decimal(9, 0); LearningCredit decimal(9, 1).
  hour: { kind: 'number', label: '時數', decimals: 0, max: 32767 },
  listPrice: { kind: 'number', label: '定價', decimals: 0, max: 999999999 },
  learningCredit: { kind: 'number', label: '點數', decimals: 1, max: 99999999.9 },
  canRepeat: { kind: 'checkbox', label: '允許重聽' },
};

/** Identifies one cell — the one being edited, or the one that was just written. */
interface EditingCell {
  pkid: number;
  field: EditableField;
}

/**
 * How long the green highlight stays on a cell that was just saved. The value only ever changes
 * after the round trip, and the editor closes at the same moment, so the highlight is the one
 * visible signal that the row was actually written.
 */
const SAVED_FLASH_MS = 1200;

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
    NgTemplateOutlet,
    TableModule,
    ButtonModule,
    DrawerModule,
    DialogModule,
    InputTextModule,
    InputNumberModule,
    CheckboxModule,
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
export class CourseList implements OnInit, OnDestroy {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

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

  // --- inline cell editing ---
  protected readonly editingCell = signal<EditingCell | null>(null);
  protected readonly editError = signal<string | null>(null);
  protected readonly savingCell = signal(false);

  /** The cell written by the last successful save; drives the fade-out highlight. */
  protected readonly savedCell = signal<EditingCell | null>(null);
  private savedFlashTimer: ReturnType<typeof setTimeout> | null = null;

  /**
   * True while a p-select / p-datepicker panel is open. Both components move focus into the
   * overlay when it opens, which fires the editor's blur — committing there would save the moment
   * the operator opened the picker. The guard defers the commit to the blur that follows the close.
   */
  protected readonly editorOverlayOpen = signal(false);

  /** One slot per editor kind; the edited column's `kind` decides which one the template binds. */
  protected draft: {
    text: string;
    number: number | null;
    date: Date | null;
    select: number | null;
    checkbox: boolean;
  } = { text: '', number: null, date: null, select: null, checkbox: false };

  // --- copy dialog ---
  protected readonly copyDialogVisible = signal(false);
  protected readonly copying = signal(false);
  protected readonly copySource = signal<Course | null>(null);
  protected readonly copyError = signal<string | null>(null);
  protected copyCourseId = '';

  ngOnDestroy(): void {
    this.clearSavedFlash();
  }

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

  // --- inline cell editing ---

  protected isEditing(course: Course, field: string): boolean {
    const cell = this.editingCell();
    return cell !== null && cell.pkid === course.pkid && cell.field === field;
  }

  protected isSaved(course: Course, field: string): boolean {
    const cell = this.savedCell();
    return cell !== null && cell.pkid === course.pkid && cell.field === field;
  }

  /**
   * Opens a cell for editing. Bound to `dblclick` only — a single click has to stay free for
   * sorting, paging and the row action buttons, which is also why PrimeNG's own `pEditableColumn`
   * is not used: that directive opens the cell from its own `click` host listener.
   */
  protected startEdit(course: Course, field: string): void {
    if (!this.isEditableField(field)) {
      return;
    }

    const current = this.editingCell();
    if (current && current.pkid === course.pkid && current.field === field) {
      return;
    }

    // A cell showing a validation error keeps the focus until the operator fixes or cancels it.
    if (this.savingCell() || (current !== null && this.editError() !== null)) {
      return;
    }

    // Ends any highlight still running. Re-adding a class an element already carries does not
    // restart its CSS animation, so without this a cell saved twice inside the flash window would
    // flash only the first time.
    this.clearSavedFlash();

    this.editingCell.set({ pkid: course.pkid, field });
    this.editError.set(null);
    this.editorOverlayOpen.set(false);
    this.loadDraft(course, field);
    this.focusEditor();
  }

  /** Escape — drops the draft and leaves the stored value on screen. */
  protected cancelEdit(): void {
    // A save already on the wire cannot be called back, and closing here would let it land — and
    // toast, and flash — behind a keystroke that reads as "cancel". The editor waits for the answer.
    if (this.savingCell()) {
      return;
    }
    this.closeEditor();
  }

  /**
   * Validates the draft and, when it differs from the stored value, persists it through the normal
   * Course update endpoint. A validation failure keeps the cell open so the value can be corrected
   * in place; a rejected save reverts the cell and reports the error.
   */
  protected commit(): void {
    const cell = this.editingCell();
    if (!cell || this.savingCell() || this.editorOverlayOpen()) {
      return;
    }

    const course = this.courses().find((c) => c.pkid === cell.pkid);
    if (!course) {
      this.closeEditor();
      return;
    }

    const value = this.readDraft(EDITABLE_COLUMNS[cell.field].kind);
    const error = this.validate(course, cell.field, value);
    if (error) {
      this.editError.set(error);
      this.focusEditor();
      return;
    }

    this.editError.set(null);
    if (this.isUnchanged(course, cell.field, value)) {
      this.closeEditor();
      return;
    }

    this.saveCell(course, cell.field, value);
  }

  private isEditableField(field: string): field is EditableField {
    return Object.prototype.hasOwnProperty.call(EDITABLE_COLUMNS, field);
  }

  private saveCell(course: Course, field: EditableField, value: EditValue): void {
    this.savingCell.set(true);

    // The list payload never carries the two n-n key arrays — they are populated by GET by pkid
    // only — and the update endpoint rewrites both junction tables from whatever the request
    // holds. Re-reading the record first is what keeps an inline edit from clearing a course's
    // certifications and job categories.
    this.service
      .getById(course.pkid)
      .pipe(
        switchMap((full) =>
          this.service.update(this.applyEdit(this.toRequest(full), field, value)),
        ),
      )
      .subscribe({
        next: (updated) => {
          this.savingCell.set(false);
          this.courses.update((rows) =>
            rows.map((row) => (row.pkid === updated.pkid ? updated : row)),
          );
          this.closeEditor();

          // The saved value is identical to what the editor was already showing, so without these
          // two the completed save produces no visible change at all. The toast says the row was
          // written; the highlight says which cell, which is what matters when several cells are
          // edited one after another.
          this.flashSaved(updated.pkid, field);
          this.messageService.add({
            severity: 'success',
            summary: '已儲存',
            // Record first, as every other toast in the app does, then the one thing a single-column
            // write has that a whole-record save does not: which column. Read from the PUT response
            // so editing 簡介代碼 or 課程名稱 reports the value just typed, not the stale one.
            detail: `${updated.courseId} ${updated.title} — ${EDITABLE_COLUMNS[field].label}`,
          });
        },
        error: (error: HttpErrorResponse) => {
          this.savingCell.set(false);
          // The row was never written optimistically, so closing the editor is the revert: the
          // cell falls back to rendering the stored value it was showing before the double-click.
          this.closeEditor();
          this.messageService.add({
            severity: 'error',
            summary: '儲存失敗',
            detail:
              error.status === 404
                ? '查無此課程，請重新整理清單。'
                : `${EDITABLE_COLUMNS[field].label}未儲存，已還原原值。`,
          });
        },
      });
  }

  /**
   * Highlights the cell just written. The previous timer is cancelled first so that editing two
   * cells in quick succession moves the highlight rather than letting the earlier one's timer cut
   * the later one short.
   */
  private flashSaved(pkid: number, field: EditableField): void {
    this.clearSavedFlash();
    this.savedCell.set({ pkid, field });
    this.savedFlashTimer = setTimeout(() => {
      this.savedCell.set(null);
      this.savedFlashTimer = null;
    }, SAVED_FLASH_MS);
  }

  private clearSavedFlash(): void {
    if (this.savedFlashTimer !== null) {
      clearTimeout(this.savedFlashTimer);
      this.savedFlashTimer = null;
    }
    this.savedCell.set(null);
  }

  private closeEditor(): void {
    this.editingCell.set(null);
    this.editError.set(null);
    this.editorOverlayOpen.set(false);
  }

  private loadDraft(course: Course, field: EditableField): void {
    this.draft = { text: '', number: null, date: null, select: null, checkbox: false };

    switch (EDITABLE_COLUMNS[field].kind) {
      case 'text':
        this.draft.text = (course[field] as string) ?? '';
        break;
      case 'number':
        this.draft.number = course[field] as number;
        break;
      case 'date':
        this.draft.date = fromIso(course[field] as string);
        break;
      case 'select':
        this.draft.select = course[field] as number;
        break;
      case 'checkbox':
        this.draft.checkbox = course[field] as boolean;
        break;
    }
  }

  private readDraft(kind: EditorKind): EditValue {
    switch (kind) {
      case 'text':
        return this.draft.text;
      case 'number':
        return this.draft.number;
      case 'date':
        return this.draft.date;
      case 'select':
        return this.draft.select;
      case 'checkbox':
        return this.draft.checkbox;
    }
  }

  /** Returns a message for an invalid edit, or null when the value may be persisted. */
  private validate(course: Course, field: EditableField, value: EditValue): string | null {
    const column = EDITABLE_COLUMNS[field];

    switch (column.kind) {
      case 'text': {
        const text = typeof value === 'string' ? value.trim() : '';
        if (text === '') {
          return `${column.label}為必填。`;
        }
        if (column.maxLength !== undefined && text.length > column.maxLength) {
          return `${column.label}不可超過 ${column.maxLength} 個字元。`;
        }
        return null;
      }

      case 'number': {
        if (typeof value !== 'number' || Number.isNaN(value)) {
          return `${column.label}為必填，且必須為數字。`;
        }
        if (value < 0) {
          return `${column.label}不可小於 0。`;
        }
        if (column.decimals === 0 && !Number.isInteger(value)) {
          return `${column.label}必須為整數。`;
        }
        if (column.decimals === 1 && Number(value.toFixed(1)) !== value) {
          return `${column.label}最多只能有 1 位小數。`;
        }
        if (column.max !== undefined && value > column.max) {
          return `${column.label}不可大於 ${column.max}。`;
        }
        return null;
      }

      case 'date': {
        if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
          return `${column.label}為必填，且必須是有效日期。`;
        }
        // The other end of the range comes from the row, so either column can be edited alone.
        const on = field === 'scheduleOn' ? toIso(value) : course.scheduleOn;
        const off = field === 'scheduleOff' ? toIso(value) : course.scheduleOff;
        if (on && off && on > off) {
          return '上架日期不可晚於下架日期。';
        }
        return null;
      }

      case 'select':
        return typeof value === 'number' ? null : `${column.label}為必填。`;

      case 'checkbox':
        return null;
    }
  }

  private isUnchanged(course: Course, field: EditableField, value: EditValue): boolean {
    if (value instanceof Date) {
      return toIso(value) === (course[field] as string);
    }
    if (typeof value === 'string') {
      return value.trim() === (course[field] as string);
    }
    return value === course[field];
  }

  /**
   * Writes the single edited column onto the request. The computed key is cast rather than
   * switched over eleven times; the field is already narrowed to a Course property by
   * `EditableField`, and a Date is serialised with local components on the way through.
   */
  private applyEdit(request: CourseRequest, field: EditableField, value: EditValue): CourseRequest {
    const patched = { ...request } as unknown as Record<string, unknown>;

    if (value instanceof Date) {
      patched[field] = toIso(value);
    } else if (typeof value === 'string') {
      patched[field] = value.trim();
    } else {
      patched[field] = value;
    }

    return patched as unknown as CourseRequest;
  }

  private toRequest(course: Course): CourseRequest {
    return {
      pkid: course.pkid,
      title: course.title,
      officialTitle: course.officialTitle,
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      friendlyUrl: course.friendlyUrl,
      displayOrder: course.displayOrder,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid,
      publishStatusPkid: course.publishStatusPkid,
      scheduleOn: course.scheduleOn,
      scheduleOff: course.scheduleOff,
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      material: course.material,
      objective: course.objective,
      target: course.target,
      prerequisites: course.prerequisites,
      outline: course.outline,
      towardCertOrExam: course.towardCertOrExam,
      note: course.note,
      otherInfo: course.otherInfo,
      canRepeat: course.canRepeat,
      certificationPkids: course.certificationPkids,
      jobCategoryPkids: course.jobCategoryPkids,
    };
  }

  /** The editor is rendered by the same change detection pass that opens the cell. */
  private focusEditor(): void {
    setTimeout(() => {
      const cell = this.host.nativeElement.querySelector<HTMLElement>('td.cell-editing');
      cell?.querySelector<HTMLElement>('input, [role="combobox"]')?.focus();
    });
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
