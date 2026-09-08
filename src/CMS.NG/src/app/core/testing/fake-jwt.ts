import { AuthProfile } from '@core/models/auth.model';

/**
 * A structurally valid, deliberately unsigned JWT for specs.
 *
 * The signature is a placeholder: nothing in the browser verifies it — the API does that — and the
 * only thing the app reads from a token is its payload claims.
 */
export function fakeJwt(claims: Record<string, unknown>): string {
  return `${base64Url({ alg: 'HS256', typ: 'JWT' })}.${base64Url(claims)}.signature-not-checked-here`;
}

/** A stored profile whose token carries the given roles, as the API would have issued it. */
export function fakeProfile(
  userId: string,
  userName: string,
  roles: string[] = [],
): AuthProfile {
  return {
    userId,
    userName,
    accessToken: fakeJwt({ userId, userName, role: roles }),
  };
}

function base64Url(value: unknown): string {
  const utf8 = encodeURIComponent(JSON.stringify(value)).replace(/%([0-9A-F]{2})/g, (_, hex) =>
    String.fromCharCode(parseInt(hex, 16)),
  );

  return btoa(utf8).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
