import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';

/** The confirm dialog renders its message as HTML, so record text must be escaped first. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-app-user-detail',
  imports: [RowAuditBadge, DatePipe, ButtonModule, TagModule, ToastModule, ConfirmDialogModule],
  providers: [MessageService, ConfirmationService],
  templateUrl: './app-user-detail.html',
  styleUrl: './app-user-detail.scss',
})
export class AppUserDetail implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly user = signal<AppUser | null>(null);
  protected readonly roles = signal<AppRoleLookup[]>([]);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly resetting = signal(false);

  ngOnInit(): void {
    const userId = this.route.snapshot.paramMap.get('id');
    if (!userId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    forkJoin({
      user: this.service.getById(userId).pipe(catchError(() => of(null))),
      roles: this.lookupService.getAppRoles().pipe(catchError(() => of([] as AppRoleLookup[]))),
    }).subscribe(({ user, roles }) => {
      this.user.set(user);
      this.roles.set(roles);
      this.notFound.set(user === null);
      this.loading.set(false);
    });
  }

  /** Assigned roles resolved to their display names, in lookup order. */
  protected get assignedRoles(): AppRoleLookup[] {
    const assigned = new Set(this.user()?.roleIds ?? []);
    return this.roles().filter((role) => assigned.has(role.roleId));
  }

  protected back(): void {
    void this.router.navigate(['/app-users']);
  }

  protected edit(): void {
    const userId = this.user()?.userId;
    if (userId) {
      void this.router.navigate(['/app-users', userId, 'edit']);
    }
  }

  /**
   * The reset always restores the SysConfig default; the new password is never displayed, because
   * it never reaches the client at all.
   */
  protected confirmResetPassword(): void {
    const user = this.user();
    if (!user) {
      return;
    }

    this.confirmationService.confirm({
      header: '重設密碼',
      message: `確定要將「${escapeHtml(user.userName)}」的密碼重設為系統預設密碼？`,
      acceptLabel: '重設',
      rejectLabel: '取消',
      accept: () => this.resetPassword(user),
    });
  }

  private resetPassword(user: AppUser): void {
    this.resetting.set(true);
    this.service.resetPassword(user.userId).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '已重設密碼',
          detail: user.userName,
        });
        this.reload(user.userId);
      },
      error: (error: { status?: number }) => {
        this.resetting.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '重設失敗',
          detail: error.status === 500 ? '系統設定缺少預設密碼，無法重設。' : '請稍後再試。',
        });
      },
    });
  }

  /** Re-fetch so 密碼更新時間 reflects the reset. */
  private reload(userId: string): void {
    this.service
      .getById(userId)
      .pipe(catchError(() => of(null)))
      .subscribe((user) => {
        if (user) {
          this.user.set(user);
        }
        this.resetting.set(false);
      });
  }
}
