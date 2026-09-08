import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AppUserService } from '@core/services/app-user.service';
import { AuthService } from '@core/services/auth.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role-lookup.model';

@Component({
  selector: 'app-app-user-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    ToggleSwitchModule,
    MultiSelectModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './app-user-form.html',
  styleUrl: './app-user-form.scss',
})
export class AppUserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly auth = inject(AuthService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly roles = signal<AppRoleLookup[]>([]);

  private userId: string | null = null;

  /**
   * No password control in either mode. A new account silently receives the SysConfig default;
   * changing it afterwards is the 重設密碼 action on the detail page.
   */
  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200)]],
    userName: ['', [Validators.required, Validators.maxLength(200)]],
    isActive: [true],
    roleIds: this.fb.nonNullable.control<string[]>([]),
  });

  ngOnInit(): void {
    this.userId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!this.userId);

    // Parallel lookup + record load.
    forkJoin({
      roles: this.lookupService.getAppRoles().pipe(catchError(() => of([] as AppRoleLookup[]))),
      user: this.userId
        ? this.service.getById(this.userId).pipe(catchError(() => of(null)))
        : of(null),
    }).subscribe(({ roles, user }) => {
      this.roles.set(roles);

      if (user) {
        this.patchFromUser(user);
      } else if (this.isEdit()) {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '查無此使用者。',
        });
      }

      // UserId is the primary key — immutable once created.
      if (this.isEdit()) {
        this.form.controls.userId.disable();
      }

      this.loading.set(false);
    });
  }

  /** Options for the 角色 multiselect. */
  protected get roleOptions(): { value: string; label: string }[] {
    return this.roles().map((role) => ({
      value: role.roleId,
      label: `${role.roleName} (${role.roleId})`,
    }));
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected cancel(): void {
    void this.router.navigate(['/app-users']);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled userId control in edit mode.
    const value = this.form.getRawValue();
    const request: AppUserRequest = {
      userId: value.userId.trim(),
      userName: value.userName.trim(),
      isActive: value.isActive,
      roleIds: value.roleIds,
    };

    this.saving.set(true);
    const save$ = this.isEdit() ? this.service.update(request) : this.service.create(request);

    save$.subscribe({
      next: (user) => {
        this.saving.set(false);

        // An administrator editing their own account renames the signed-in operator. The session
        // holds the name from login, so without this the app shell keeps showing the old one until
        // the next sign-in. A no-op for every other account.
        this.auth.syncUserName(user.userId, user.userName);

        this.messageService.add({ severity: 'success', summary: '已儲存', detail: user.userName });
        void this.router.navigate(['/app-users', user.userId]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: this.saveErrorDetail(error.status),
        });
      },
    });
  }

  /** 500 is the one place the operator meets the SysConfig dependency. */
  private saveErrorDetail(status: number): string {
    if (status === 409) {
      return '使用者代碼已存在。';
    }
    if (status === 500) {
      return '系統設定缺少預設密碼，無法建立帳號。';
    }
    return '請稍後再試。';
  }

  private patchFromUser(user: AppUser): void {
    this.form.patchValue({
      userId: user.userId,
      userName: user.userName,
      isActive: user.isActive,
      roleIds: user.roleIds,
    });
  }
}
