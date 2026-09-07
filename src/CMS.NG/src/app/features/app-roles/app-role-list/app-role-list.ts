import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';

import { AppRoleService } from '@core/services/app-role.service';
import { AppRole, AppRoleQuery } from '@core/models/app-role.model';

const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

interface ListSort {
  field: string;
  order: number;
}

interface ListPage {
  first: number;
  rows: number;
}

@Component({
  selector: 'app-app-role-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    InputNumberModule,
    ToastModule,
    ConfirmDialogModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss',
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Draft filter values bound to the drawer; applied only on 搜尋. */
  protected draftFilters: AppRoleQuery = { keyword: null, permissionLevel: null };
  protected appliedFilters: AppRoleQuery = { keyword: null, permissionLevel: null };

  protected sort: ListSort = { field: 'roleId', order: 1 };
  protected page: ListPage = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  protected get hasActiveFilters(): boolean {
    return (
      !!this.appliedFilters.keyword?.trim() ||
      this.appliedFilters.permissionLevel !== null &&
        this.appliedFilters.permissionLevel !== undefined
    );
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.roles.set([]);
        this.loading.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法取得角色清單。',
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
    this.draftFilters = { keyword: null, permissionLevel: null };
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
    void this.router.navigate(['/app-roles/new']);
  }

  protected view(role: AppRole): void {
    void this.router.navigate(['/app-roles', role.roleId]);
  }

  protected edit(role: AppRole): void {
    void this.router.navigate(['/app-roles', role.roleId, 'edit']);
  }

  protected confirmDelete(role: AppRole): void {
    this.confirmationService.confirm({
      header: '刪除角色',
      message: `確定要刪除角色「${role.roleName}」嗎？`,
      acceptLabel: '刪除',
      rejectLabel: '取消',
      accept: () => this.delete(role),
    });
  }

  private delete(role: AppRole): void {
    this.service.delete(role.roleId).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: '已刪除', detail: role.roleName });
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: '此角色可能仍有使用者關聯。',
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
