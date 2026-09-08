import { Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

import { Router } from '@angular/router';

import { AuthService } from '@core/services/auth.service';
import {
  LOGIN_REASON_PARAM,
  LOGIN_ROUTE,
  PASSWORD_CHANGED_REASON,
} from '@core/guards/auth.guard';

/**
 * 密碼原則 — the same rule PasswordPolicy enforces on the server, stated here so the operator is
 * told before the round trip rather than after it. This is convenience, never protection: the API
 * re-checks every request, and these two copies are expected to read alike.
 */
const PASSWORD_MIN_LENGTH = 8;
const PASSWORD_REQUIRED_CLASSES = 3;

/** Word for word what the API answers with, so the two never disagree on screen. */
export const PASSWORD_RULE_MESSAGE =
  '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號';

/** The English half of the same sentence — the labels here are bilingual, and so is this. */
export const PASSWORD_RULE_MESSAGE_EN =
  'Password must be at least 8 characters and contain at least 3 of the 4 classes: ' +
  'uppercase / lowercase / digit / symbol.';

/**
 * Long enough, and spanning enough character classes. "Symbol" is everything that is not an
 * uppercase letter, a lowercase letter or a digit — the server counts it the same way, which is
 * what lets a 中文字 or a space stand as the third class.
 *
 * An empty value is left to `Validators.required`, so a blank field does not show the rule as
 * though the operator had typed something wrong.
 */
export function passwordComplexity(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');
  if (!value) {
    return null;
  }

  const classes =
    (/\p{Lu}/u.test(value) ? 1 : 0) +
    (/\p{Ll}/u.test(value) ? 1 : 0) +
    (/\p{Nd}/u.test(value) ? 1 : 0) +
    (/[^\p{Lu}\p{Ll}\p{Nd}]/u.test(value) ? 1 : 0);

  return value.length >= PASSWORD_MIN_LENGTH && classes >= PASSWORD_REQUIRED_CLASSES
    ? null
    : { complexity: true };
}

/**
 * 新密碼 and 確認新密碼 must be identical. A group validator rather than a control one, because
 * neither field is wrong on its own — and it stays quiet until both have been filled in, so the
 * mismatch is not shouted at an operator halfway through typing the confirmation.
 */
export function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPassword = String(group.get('newPassword')?.value ?? '');
  const confirmNewPassword = String(group.get('confirmNewPassword')?.value ?? '');

  if (!newPassword || !confirmNewPassword) {
    return null;
  }

  return newPassword === confirmNewPassword ? null : { mismatch: true };
}

/**
 * 個人資料 My Profile — the one page an operator may edit about their own account.
 *
 * Two independent forms, and two separate writes. 帳號資料 renames the operator: only 使用者名稱
 * is writable, because 使用者代碼 is the primary key and 角色 is an administrator's decision.
 * 變更密碼 replaces the password. Neither travels with a key — PUT /api/auth/profile and
 * POST /api/auth/change-password both take the account from the token.
 *
 * Two `<form>` elements rather than one, because forms do not nest and each has its own submit
 * button: saving a new name must not send a password, and vice versa.
 *
 * Nothing here hashes anything. The three password fields hold plaintext on their way to the API
 * and are cleared the moment it answers; a PasswordHash neither arrives nor leaves.
 *
 * A successful password change **ends the session and returns to 登入**. The API stops honouring
 * every token issued before the change (`TokenFreshness`), so the stored one is already dead by
 * the time this handler runs — clearing it and leaving is what turns a string of 401s into an
 * orderly sign-out. AuthService drops the token; this page does the navigating.
 */
@Component({
  selector: 'app-profile',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    TagModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly saving = signal(false);
  protected readonly changingPassword = signal(false);

  /** Rendered under 新密碼, as a hint while the field is clean and as the error once it is not. */
  protected readonly passwordRule = PASSWORD_RULE_MESSAGE;
  protected readonly passwordRuleEn = PASSWORD_RULE_MESSAGE_EN;

  /** 使用者代碼 of the signed-in operator — from the stored session, as the shell reads it. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');

  /** 角色 — out of the token's claims, the same source the sidebar's role gate uses. */
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    userId: [''],
    userName: ['', [Validators.required, Validators.maxLength(200)]],
  });

  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordComplexity]],
      confirmNewPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

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

  protected isPasswordInvalid(controlName: keyof typeof this.passwordForm.controls): boolean {
    const control = this.passwordForm.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  /** True once both new-password fields have been touched and still disagree. */
  protected get showsMismatch(): boolean {
    const confirm = this.passwordForm.controls.confirmNewPassword;
    return this.passwordForm.hasError('mismatch') && (confirm.dirty || confirm.touched);
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

  protected changePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    // Sent verbatim — never trimmed. Leading or trailing whitespace is part of a password, and
    // trimming it here would hash something the operator did not type.
    const request = this.passwordForm.getRawValue();

    this.changingPassword.set(true);
    this.auth.changePassword(request).subscribe({
      next: () => {
        this.changingPassword.set(false);

        // Nothing is left on screen: three plaintext passwords have no reason to outlive the
        // request that carried them.
        this.clearPasswordForm();

        // AuthService has already dropped the session — the token was issued against a password
        // that no longer exists, and the API will no longer accept it — so this page is now behind
        // the guard with nothing to render.
        // Back to 登入, carrying the reason so the Login page can say why rather than looking
        // like an unexplained sign-out. No toast: this component is torn down by the navigation,
        // and a message the operator never sees is worse than none.
        void this.router.navigate([LOGIN_ROUTE], {
          queryParams: { [LOGIN_REASON_PARAM]: PASSWORD_CHANGED_REASON },
        });
      },
      error: (error: HttpErrorResponse) => {
        this.changingPassword.set(false);

        // The fields are left as they are: whichever gate refused, the operator is about to
        // correct one of them, and clearing the form would make them retype all three.
        this.messageService.add({
          severity: 'error',
          summary: '變更失敗',
          detail: this.changePasswordErrorDetail(error),
        });
      },
    });
  }

  private clearPasswordForm(): void {
    this.passwordForm.reset({
      currentPassword: '',
      newPassword: '',
      confirmNewPassword: '',
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

  /**
   * The API answers every rejection as a ProblemDetails whose `title` is already the Chinese
   * wording the operator needs — 目前密碼錯誤, the complexity rule, or the mismatch. Preferring it
   * over a locally composed message keeps the two sides from drifting apart.
   */
  private changePasswordErrorDetail(error: HttpErrorResponse): string {
    if (error.status === 404) {
      return '查無此帳號，請重新登入。';
    }

    const title = (error.error as { title?: unknown } | null)?.title;
    if (error.status === 400 && typeof title === 'string' && title.trim()) {
      return title;
    }

    if (error.status === 400) {
      return '請確認輸入內容是否正確。';
    }

    return '請稍後再試。';
  }
}
