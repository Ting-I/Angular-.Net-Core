import { Component, signal } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { NgClass } from '@angular/common';

/** Sidebar nav group — mirrors the UI sample's collapsible groups. */
export interface NavGroup {
  label: string;
  icon: string;
  items: NavItem[];
}

export interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgClass],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = signal('UWA');
  protected readonly sidebarCollapsed = signal(false);

  /** Only groups with implemented features carry routes; the rest are placeholders. */
  protected readonly navGroups = signal<NavGroup[]>([
    { label: '首頁管理 Home', icon: 'pi pi-home', items: [] },
    { label: '課程管理 Course', icon: 'pi pi-folder', items: [] },
    { label: '說明會 Seminar', icon: 'pi pi-comments', items: [] },
    { label: '活動管理 Promotion', icon: 'pi pi-megaphone', items: [] },
    { label: '線上報名 Forms', icon: 'pi pi-file-edit', items: [] },
    { label: '網站資訊 WebInfo', icon: 'pi pi-globe', items: [] },
    { label: '考試中心 TestingCenter', icon: 'pi pi-check-circle', items: [] },
    {
      label: '系統管理 Admin',
      icon: 'pi pi-shield',
      items: [
        { label: '角色 AppRole', icon: 'pi pi-id-card', route: '/app-roles' },
        { label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' },
      ],
    },
  ]);

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
}
