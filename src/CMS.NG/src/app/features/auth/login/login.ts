import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';

import { AuthService } from '@core/services/auth.service';

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

  protected readonly signingIn = signal(false);

  /**
   * The failure message, shown in the form rather than as a toast: it belongs next to the fields
   * that produced it, and the operator is about to retype one of them.
   */
  protected readonly error = signal<string | null>(null);

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
