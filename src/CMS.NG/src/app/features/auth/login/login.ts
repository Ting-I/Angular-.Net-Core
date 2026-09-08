import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';

import { AuthService } from '@core/services/auth.service';
import { LOGIN_REASON_PARAM, PASSWORD_CHANGED_REASON } from '@core/guards/auth.guard';

/** 登入 Login — the only page reachable without a token. */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly signingIn = signal(false);

  /**
   * The failure message, shown in the form rather than as a toast: it belongs next to the fields
   * that produced it, and the operator is about to retype one of them.
   */
  protected readonly error = signal<string | null>(null);

  /**
   * Why the operator is looking at this page when they did not ask to be. Read once from the
   * query string rather than subscribed: nothing navigates between /login and itself, and the
   * parameter surviving in the URL means a reload still explains the sign-out.
   */
  protected readonly notice = signal<string | null>(
    this.route.snapshot.queryParamMap.get(LOGIN_REASON_PARAM) === PASSWORD_CHANGED_REASON
      ? '密碼已變更，請使用新密碼重新登入。'
      : null,
  );

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200)]],
    password: ['', [Validators.required]],
  });

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected signIn(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { userId, password } = this.form.getRawValue();

    this.signingIn.set(true);
    this.error.set(null);

    // The notice explained a sign-out that is now history; leaving it above a fresh
    // 帳號或密碼錯誤 would read as though both applied to this attempt.
    this.notice.set(null);

    this.auth.login({ userId: userId.trim(), password }).subscribe({
      next: () => {
        this.signingIn.set(false);
        void this.router.navigateByUrl('/');
      },
      error: (error: HttpErrorResponse) => {
        this.signingIn.set(false);

        // The API answers the same 401 for an unknown account, a disabled one and a wrong
        // password, and so does this: saying which it was would leak whether the account exists.
        this.error.set(
          error.status === 401 ? '帳號或密碼錯誤。' : '無法登入，請稍後再試。',
        );

        this.form.controls.password.reset();
      },
    });
  }
}
