import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user-lookup.model';

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-app-role-detail',
  imports: [RowAuditBadge, ButtonModule, TagModule],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss',
})
export class AppRoleDetail implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly users = signal<AppUserLookup[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    const roleId = this.route.snapshot.paramMap.get('id');
    if (!roleId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    forkJoin({
      role: this.service.getById(roleId).pipe(catchError(() => of(null))),
      users: this.lookupService.getAppUsers().pipe(catchError(() => of([] as AppUserLookup[]))),
    }).subscribe(({ role, users }) => {
      this.role.set(role);
      this.users.set(users);
      this.notFound.set(role === null);
      this.loading.set(false);
    });
  }

  /** Assigned users resolved to their display names, sorted for stable rendering. */
  protected get assignedUsers(): AppUserLookup[] {
    const assigned = new Set(this.role()?.userIds ?? []);
    return this.users().filter((user) => assigned.has(user.userId));
  }

  protected back(): void {
    void this.router.navigate(['/app-roles']);
  }

  protected edit(): void {
    const roleId = this.role()?.roleId;
    if (roleId) {
      void this.router.navigate(['/app-roles', roleId, 'edit']);
    }
  }
}
