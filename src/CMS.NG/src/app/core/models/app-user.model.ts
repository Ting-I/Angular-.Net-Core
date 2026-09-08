/**
 * 使用者 AppUser — response model from GET /api/app-users.
 *
 * There is no password field anywhere in this file, by design: PasswordHash is written by the
 * server only and never crosses the wire in either direction.
 */
export interface AppUser {
  /** 主代碼 — non-key identity column */
  pkid: number;
  /** 使用者代碼 (primary key) */
  userId: string;
  /** 使用者名稱 */
  userName: string;
  /** 啟用 */
  isActive: boolean;
  /** 密碼更新時間 — UTC, display only; null until the hash is first written. */
  passwordUpdatedTime: string | null;
  /** 角色數 */
  roleCount: number;
  /** 角色 — populated on GET by id only. */
  roleIds: string[];
}

/** 使用者 AppUser — write DTO for POST / PUT. Carries no password of any kind. */
export interface AppUserRequest {
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}

/** 使用者 AppUser — search DTO for POST /api/app-users/query. */
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
  roleId?: string | null;
  passwordUpdatedFrom?: string | null;
  passwordUpdatedTo?: string | null;
}
