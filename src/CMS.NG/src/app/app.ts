import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { NgClass } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';

import { ADMIN_ROLE, AuthService } from '@core/services/auth.service';
import { LOGIN_ROUTE } from '@core/guards/auth.guard';

/** Sidebar nav group — mirrors the UI sample's collapsible groups. */
export interface NavGroup {
  label: string;
  icon: string;
  items: NavItem[];
  /** When set, the group is only rendered for a user holding this role. */
  requiresRole?: string;
}

export interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgClass, ButtonModule, ToastModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly title = signal('UWA');
  protected readonly sidebarCollapsed = signal(false);

  /** 使用者名稱 of the signed-in operator, shown in the top bar. */
  protected readonly userName = this.auth.userName;

  /** Signed out, the shell is not drawn at all — the Login page owns the viewport. */
  protected readonly signedIn = this.auth.isAuthenticated;

  /** Only groups with implemented features carry routes; the rest are placeholders. */
  private readonly allNavGroups = signal<NavGroup[]>([
    {
      label: '首頁管理 Home',
      icon: 'pi pi-home',
      items: [
        {
          label: '上稿作業 FeaturedPromoItem',
          icon: 'pi pi-calendar',
          route: '/featured-promo-items',
        },
      ],
    },
    {
      label: '課程管理 Course',
      icon: 'pi pi-folder',
      items: [
        { label: '原廠 Partner', icon: 'pi pi-building', route: '/partners' },
        { label: '課程群組 CourseGroup', icon: 'pi pi-sitemap', route: '/course-groups' },
        { label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
      ],
    },
    { label: '說明會 Seminar', icon: 'pi pi-comments', items: [] },
    { label: '活動管理 Promotion', icon: 'pi pi-megaphone', items: [] },
    { label: '線上報名 Forms', icon: 'pi pi-file-edit', items: [] },
    { label: '網站資訊 WebInfo', icon: 'pi pi-globe', items: [] },
    { label: '考試中心 TestingCenter', icon: 'pi pi-check-circle', items: [] },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      requiresRole: ADMIN_ROLE,
      items: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
        { label: '使用者 AppUser', icon: 'pi pi-users', route: '/app-users' },
      ],
    },
  ]);

  /**
   * What this operator may see. Hiding 系統管理 Admin is presentation, not protection — the API
   * decides who may call its endpoints; this only keeps a menu the user cannot use off the screen.
   */
  protected readonly navGroups = computed(() => {
    const roles = this.auth.roles();
    return this.allNavGroups().filter(
      (group) => !group.requiresRole || roles.includes(group.requiresRole),
    );
  });

  protected readonly expandedGroups = signal<Set<string>>(new Set(['系統管理 Admin']));

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

  protected toggleGroup(label: string): void {
    this.expandedGroups.update((groups) => {
      const next = new Set(groups);
      if (next.has(label)) {
        next.delete(label);
      } else {
        next.add(label);
      }
      return next;
    });
  }

  protected isExpanded(label: string): boolean {
    return this.expandedGroups().has(label);
  }

  /** 登出 — drops the session and returns to the Login page. */
  protected logout(): void {
    this.auth.logout();
    void this.router.navigate([LOGIN_ROUTE]);
  }
}
