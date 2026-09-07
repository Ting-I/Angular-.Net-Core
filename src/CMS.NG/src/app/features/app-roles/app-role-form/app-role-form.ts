import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppUserLookup } from '@core/models/app-user-lookup.model';

@Component({
  selector: 'app-app-role-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    MultiSelectModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss',
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly users = signal<AppUserLookup[]>([]);

  private roleId: string | null = null;

  protected readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required, Validators.maxLength(200)]],
    roleName: ['', [Validators.required, Validators.maxLength(200)]],
    permissionLevel: [100, [Validators.required, Validators.min(0)]],
    description: this.fb.control<string | null>(null, [Validators.maxLength(400)]),
    userIds: this.fb.nonNullable.control<string[]>([]),
  });

  ngOnInit(): void {
    this.roleId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!this.roleId);

    // Parallel lookup + record load.
    forkJoin({
      users: this.lookupService.getAppUsers().pipe(catchError(() => of([] as AppUserLookup[]))),
      role: this.roleId
        ? this.service.getById(this.roleId).pipe(catchError(() => of(null)))
        : of(null),
    }).subscribe(({ users, role }) => {
      this.users.set(users);

      if (role) {
        this.patchFromRole(role);
      } else if (this.isEdit()) {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '查無此角色。',
        });
      }

      // RoleId is the primary key — immutable once created.
      if (this.isEdit()) {
        this.form.controls.roleId.disable();
      }

      this.loading.set(false);
    });
  }

  /** Options for the 使用者 multiselect. */
  protected get userOptions(): { value: string; label: string }[] {
    return this.users().map((user) => ({
      value: user.userId,
      label: `${user.userName} (${user.userId})`,
    }));
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected cancel(): void {
    void this.router.navigate(['/app-roles']);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled roleId control in edit mode.
    const value = this.form.getRawValue();
    const request: AppRoleRequest = {
      roleId: value.roleId.trim(),
      roleName: value.roleName.trim(),
      permissionLevel: value.permissionLevel,
      description: value.description?.trim() || null,
      userIds: value.userIds,
    };

    this.saving.set(true);
    const save$ = this.isEdit() ? this.service.update(request) : this.service.create(request);

    save$.subscribe({
      next: (role) => {
        this.saving.set(false);
        this.messageService.add({ severity: 'success', summary: '已儲存', detail: role.roleName });
        void this.router.navigate(['/app-roles', role.roleId]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: error.status === 409 ? '角色代碼已存在。' : '請稍後再試。',
        });
      },
    });
  }

  private patchFromRole(role: AppRole): void {
    this.form.patchValue({
      roleId: role.roleId,
      roleName: role.roleName,
      permissionLevel: role.permissionLevel,
      description: role.description,
      userIds: role.userIds,
    });
  }
}
