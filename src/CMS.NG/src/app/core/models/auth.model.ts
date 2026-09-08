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
