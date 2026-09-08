import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

import { AuthService } from '@core/services/auth.service';

/**
 * 個人資料 My Profile — the one page an operator may edit about their own account.
 *
 * Only 使用者名稱 is writable. 使用者代碼 is the primary key, and 角色 is an administrator's
 * decision: both are rendered, neither is sent. That is presentation, not protection —
 * PUT /api/auth/profile takes the account from the token and has no property for either.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, TagModule, ToastModule],
  providers: [MessageService],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  protected readonly saving = signal(false);

  /** 使用者代碼 of the signed-in operator — from the stored session, as the shell reads it. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');

  /** 角色 — out of the token's claims, the same source the sidebar's role gate uses. */
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    userId: [''],
    userName: ['', [Validators.required, Validators.maxLength(200)]],
  });

  constructor() {
    this.form.patchValue({
      userId: this.userId(),
      userName: this.auth.userName(),
    });

    // The key is immutable, here as everywhere else — the control is display only.
    this.form.controls.userId.disable();
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  /** Puts the form back to what the session holds, discarding an unsaved edit. */
  protected reset(): void {
    this.form.patchValue({ userName: this.auth.userName() });
    this.form.controls.userName.markAsPristine();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // Only 使用者名稱 travels; the disabled userId control is not in the value at all.
    const userName = this.form.controls.userName.value.trim();
    if (!userName) {
      this.form.controls.userName.setErrors({ required: true });
      this.form.controls.userName.markAsTouched();
      return;
    }

    this.saving.set(true);
    this.auth.updateProfile({ userName }).subscribe({
      next: (profile) => {
        this.saving.set(false);

        // AuthService has already folded the name into the session, so the top bar is showing it
        // by now; the form takes the server's copy so a trimmed name is visible as it was stored.
        this.form.patchValue({ userName: profile.userName });
        this.form.controls.userName.markAsPristine();

        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: profile.userName,
        });
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

  private saveErrorDetail(status: number): string {
    if (status === 400) {
      return '使用者名稱為必填。';
    }
    if (status === 404) {
      return '查無此帳號，請重新登入。';
    }
    return '請稍後再試。';
  }
}
