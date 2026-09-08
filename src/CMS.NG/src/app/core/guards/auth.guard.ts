import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';

import { AuthService } from '@core/services/auth.service';

/** Where an unauthenticated visitor lands. The only public route in the app. */
export const LOGIN_ROUTE = '/login';

/**
 * Query parameter on /login saying why the operator was sent back, so the page can explain
 * itself rather than looking like an unexplained sign-out.
 *
 * It lives here beside LOGIN_ROUTE because both ends need it and neither owns the other: the
 * Profile page writes it, the Login page reads it — the same shape as the route constant the
 * interceptor and the guard share.
 */
export const LOGIN_REASON_PARAM = 'reason';

/** 變更密碼 — the session was dropped because the password it was signed in with is gone. */
export const PASSWORD_CHANGED_REASON = 'password-changed';

/**
 * Blocks every route under the app shell when session storage holds no token.
 *
 * Attached once as `canActivateChild` on the shell's empty-path parent rather than repeated on
 * each child, so a route added later is guarded by default. It returns a `UrlTree` instead of
 * navigating, which is what lets the router treat the redirect as part of the same navigation.
 */
export const authGuard: CanActivateChildFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() || router.createUrlTree([LOGIN_ROUTE]);
};
