import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { environment } from '@env';
import {
  AuthProfile,
  ChangePasswordRequest,
  LoginRequest,
  ProfileRequest,
  UserProfile,
} from '@core/models/auth.model';

/**
 * Session storage, not local storage: the sign-in dies with the browser tab, so a shared machine
 * does not hand the next person a live token.
 */
const PROFILE_KEY = 'auth-profile';

/** The role that unlocks 系統管理 Admin. Matches AppRole.RoleId, which the token carries. */
export const ADMIN_ROLE = 'Admin';

/** The token's role claim — JwtTokenService.RoleClaimType on the server. */
const ROLE_CLAIM = 'role';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  /** Seeded from storage so a page reload keeps the operator signed in. */
  private readonly stored = signal<AuthProfile | null>(readProfile());

  /** 使用者 — the signed-in profile, or null. */
  readonly profile = this.stored.asReadonly();

  readonly accessToken = computed(() => this.stored()?.accessToken ?? null);

  readonly userName = computed(() => this.stored()?.userName ?? '');

  readonly isAuthenticated = computed(() => !!this.accessToken());

  /** 角色 — read out of the token's claims, never from a separate API call. */
  readonly roles = computed(() => rolesFromToken(this.accessToken()));

  readonly isAdmin = computed(() => this.roles().includes(ADMIN_ROLE));

  /** Signs in and stores the profile. A failure stores nothing and leaves any old session cleared. */
  login(request: LoginRequest): Observable<AuthProfile> {
    return this.http
      .post<AuthProfile>(`${this.baseUrl}/login`, request)
      .pipe(tap((profile) => this.store(profile)));
  }

  /**
   * 個人資料 — renames the signed-in operator, and folds the new 使用者名稱 back into the live
   * session so the app shell shows it without a re-login.
   *
   * The server picks the account from the token, so the request carries no key. The stored token
   * is left exactly as it was: its `userName` claim is now stale, but nothing reads a name from
   * the claims — only the roles come from there — and re-signing it would mean a second login.
   */
  updateProfile(request: ProfileRequest): Observable<UserProfile> {
    return this.http
      .put<UserProfile>(`${this.baseUrl}/profile`, request)
      .pipe(tap((profile) => this.storeUserName(profile.userName)));
  }

  /**
   * 變更密碼 — replaces the signed-in operator's own password.
   *
   * The account is the token's, so the request carries no key, and the API answers 204 with no
   * body: no hash travels in either direction.
   *
   * **A success ends the session.** The API refuses the stored token from here on — it was signed
   * before the password changed, and `TokenFreshness` compares every token's `iat` against
   * `AppUser.PasswordUpdatedTime` — so keeping it would only mean the next request answering 401
   * and the interceptor tidying up after the fact. Dropping it from this side rather than from
   * the page means no caller can forget to, the same reason `updateProfile` folds the new name in
   * here. Navigation is the caller's business, exactly as it is for `logout`.
   *
   * A failure leaves the session alone: nothing changed, and the operator is mid-retry.
   */
  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/change-password`, request)
      .pipe(tap(() => this.clearSession()));
  }

  /**
   * Keeps the session in step when the signed-in operator is renamed from somewhere other than
   * 個人資料 — today that is 使用者 AppUser, whose form writes `PUT /api/app-users` and would
   * otherwise leave the app shell showing the name from login until the next sign-in.
   *
   * A no-op for anybody else's account, so the AppUser form can call it after every save without
   * asking whose row it just wrote. UserId is compared case-insensitively, because SQL Server's
   * default collation is and the API looks the key up the same way.
   */
  syncUserName(userId: string, userName: string): void {
    const current = this.stored();
    if (current?.userId.toLowerCase() === userId.toLowerCase()) {
      this.storeUserName(userName);
    }
  }

  /** 登出 — drops the session. Navigation is the caller's business. */
  logout(): void {
    this.clearSession();
  }

  /**
   * Drops the session without any of the ceremony around it. What the interceptor calls when the
   * API rejects a token: whatever is in storage is no longer worth holding.
   */
  clearSession(): void {
    sessionStorage.removeItem(PROFILE_KEY);
    this.stored.set(null);
  }

  private store(profile: AuthProfile): void {
    sessionStorage.setItem(PROFILE_KEY, JSON.stringify(profile));
    this.stored.set(profile);
  }

  /**
   * Replaces the name in the stored session, keeping the key and the token. A signed-out service
   * has nothing to rename — a 401 would have cleared the session before the response landed.
   */
  private storeUserName(userName: string): void {
    const current = this.stored();
    if (current) {
      this.store({ ...current, userName });
    }
  }
}

/** The stored profile, or null when there is none — or when what is stored is not one. */
function readProfile(): AuthProfile | null {
  const raw = sessionStorage.getItem(PROFILE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<AuthProfile>;
    return parsed?.accessToken ? (parsed as AuthProfile) : null;
  } catch {
    // Hand-edited or half-written storage reads as signed out rather than crashing the app.
    return null;
  }
}

/**
 * The `role` claims of a JWT payload. A single role serializes as a string and several as an
 * array, so both shapes have to be accepted.
 *
 * The payload is read, not trusted: the API validates the signature on every request, and nothing
 * here is a security decision — it only decides which menu entries are worth rendering.
 */
export function rolesFromToken(accessToken: string | null): string[] {
  const payload = decodePayload(accessToken);
  if (!payload) {
    return [];
  }

  const claim = payload[ROLE_CLAIM];
  if (typeof claim === 'string') {
    return [claim];
  }

  return Array.isArray(claim) ? claim.filter((role): role is string => typeof role === 'string') : [];
}

function decodePayload(accessToken: string | null): Record<string, unknown> | null {
  const segment = accessToken?.split('.')[1];
  if (!segment) {
    return null;
  }

  try {
    // base64url → base64, then pad to a multiple of four for atob.
    const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');

    // decodeURIComponent(escape(...)) is what turns atob's latin-1 bytes back into UTF-8, which a
    // 使用者名稱 in the payload needs.
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((char) => `%${`00${char.charCodeAt(0).toString(16)}`.slice(-2)}`)
        .join(''),
    );

    const parsed: unknown = JSON.parse(json);
    return typeof parsed === 'object' && parsed !== null ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}
