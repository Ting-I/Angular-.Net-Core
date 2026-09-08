import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppUserQuery } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';
import { fromIso, toIso } from '@core/utils/date.util';

const FILTERS_KEY = 'app-user-list-filters';
const SORT_KEY = 'app-user-list-sort';
const PAGE_KEY = 'app-user-list-page';

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
  isActive: boolean | null;
  roleId: string | null;
  passwordUpdatedFrom: Date | null;
  passwordUpdatedTo: Date | null;
}

/** The confirm dialog renders its message as HTML, so record text must be escaped first. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

const EMPTY_FILTERS: AppUserQuery = {
  keyword: null,
  isActive: null,
  roleId: null,
  passwordUpdatedFrom: null,
  passwordUpdatedTo: null,
};

@Component({
  selector: 'app-app-user-list',
  imports: [
    FormsModule,
    DatePipe,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    DatePickerModule,
    ToastModule,
    ConfirmDialogModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './app-user-list.html',
  styleUrl: './app-user-list.scss',
})
export class AppUserList implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly users = signal<AppUser[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Drawer select options for the 角色 filter. */
  protected readonly roles = signal<AppRoleLookup[]>([]);

  /** Tri-state options for the 啟用 filter: null keeps the filter off. */
  protected readonly isActiveOptions = [
    { label: '全部', value: null },
    { label: '啟用', value: true },
    { label: '停用', value: false },
  ];

  protected draftFilters: DraftFilters = this.toDraft(EMPTY_FILTERS);
  protected appliedFilters: AppUserQuery = { ...EMPTY_FILTERS };

  protected sort: ListSort = { field: 'userId', order: 1 };
  protected page: ListPage = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.applyRouteParams();

    // The role lookup only populates the drawer, but restored filters need its labels, so the
    // first query waits for it. A failed lookup still leaves the list usable.
    this.lookupService
      .getAppRoles()
      .pipe(
        catchError(() => {
          this.messageService.add({
            severity: 'error',
            summary: '載入失敗',
            detail: '無法取得篩選選項。',
          });
          return of([] as AppRoleLookup[]);
        }),
      )
      .subscribe((roles) => {
        this.roles.set(roles);
        this.load();
      });
  }

  protected get roleOptions(): { roleId: string; label: string }[] {
    return this.roles().map((r) => ({ roleId: r.roleId, label: `${r.roleName} (${r.roleId})` }));
  }

  /** Number of applied filters — shown as the 搜尋條件 button badge. */
  protected get activeFilterCount(): number {
    const f = this.appliedFilters;
    const set = (value: unknown): number => (value !== null && value !== undefined ? 1 : 0);

    return (
      (f.keyword?.trim() ? 1 : 0) +
      // isActive === false (停用) is a real filter; a falsy check would silently drop it.
      set(f.isActive) +
      (f.roleId?.trim() ? 1 : 0) +
      set(f.passwordUpdatedFrom) +
      set(f.passwordUpdatedTo)
    );
  }

  protected get hasActiveFilters(): boolean {
    return this.activeFilterCount > 0;
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => {
        this.users.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得使用者清單。',
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
    void this.router.navigate(['/app-users/new']);
  }

  protected view(user: AppUser): void {
    void this.router.navigate(['/app-users', user.userId]);
  }

  protected edit(user: AppUser): void {
    void this.router.navigate(['/app-users', user.userId, 'edit']);
  }

  protected confirmDelete(user: AppUser): void {
    // A statement of consequence, not a blocker: the junction rows go with the user.
    const warning =
      user.roleCount > 0 ? `<br>此使用者仍有 ${user.roleCount} 個角色關聯，將一併刪除。` : '';

    // p-confirmDialog renders the message with [innerHTML], so the record text is escaped.
    this.confirmationService.confirm({
      header: '刪除使用者',
      message:
        `確定要刪除主代碼 <b>${user.pkid}</b>` +
        `「${escapeHtml(user.userId)} ${escapeHtml(user.userName)}」？${warning}`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(user),
    });
  }

  private delete(user: AppUser): void {
    this.service.delete(user.userId).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已刪除',
          detail: user.userName,
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

  /**
   * Cross-entity navigation from AppRole. An incoming roleId replaces that one saved filter and
   * leaves the rest of the restored state alone.
   */
  private applyRouteParams(): void {
    const roleId = this.route.snapshot.queryParamMap.get('roleId');
    if (roleId === null) {
      return;
    }

    this.appliedFilters = { ...this.appliedFilters, roleId };
    this.draftFilters = this.toDraft(this.appliedFilters);
    this.page = { ...this.page, first: 0 };
    this.persistState();
  }

  private toDraft(filters: AppUserQuery): DraftFilters {
    return {
      keyword: filters.keyword ?? null,
      isActive: filters.isActive ?? null,
      roleId: filters.roleId ?? null,
      passwordUpdatedFrom: fromIso(filters.passwordUpdatedFrom),
      passwordUpdatedTo: fromIso(filters.passwordUpdatedTo),
    };
  }

  private fromDraft(draft: DraftFilters): AppUserQuery {
    return {
      keyword: draft.keyword,
      isActive: draft.isActive,
      roleId: draft.roleId,
      passwordUpdatedFrom: toIso(draft.passwordUpdatedFrom),
      passwordUpdatedTo: toIso(draft.passwordUpdatedTo),
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
