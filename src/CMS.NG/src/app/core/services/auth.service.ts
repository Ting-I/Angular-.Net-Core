import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { environment } from '@env';
import { AuthProfile, LoginRequest } from '@core/models/auth.model';

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
