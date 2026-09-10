/** 登入 — body of POST /api/auth/login. */
export interface LoginRequest {
  userId: string;
  password: string;
}

/**
 * 登入結果 — exactly what the API returns, and exactly what is kept in session storage.
 * The roles are not a field here: they travel inside the token, and `AuthService` reads them
 * from its claims rather than from a second API call.
 */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
}

/**
 * 個人資料 — body of PUT /api/auth/profile.
 *
 * 使用者名稱 alone, mirroring the server's `ProfileRequest`. There is deliberately no `userId`:
 * the server takes the account from the token, and a key sent here would be discarded anyway.
 */
export interface ProfileRequest {
  userName: string;
}

/** 個人資料 — what PUT /api/auth/profile answers with. `roleIds` is display only. */
export interface UserProfile {
  userId: string;
  userName: string;
  roleIds: string[];
}

/**
 * 變更密碼 — body of POST /api/auth/change-password.
 *
 * Three plaintext passwords and, as with `ProfileRequest`, deliberately no `userId`: the server
 * takes the account from the token. Nothing is hashed here — the browser never computes, holds or
 * receives a `PasswordHash`.
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}
