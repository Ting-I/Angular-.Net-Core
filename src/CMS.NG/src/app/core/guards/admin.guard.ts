import { inject } from '@angular/core';
import { CanActivateChildFn, Router, UrlTree } from '@angular/router';

import { AuthService } from '@core/services/auth.service';

/** Where 系統管理 Admin starts, and where an administrator lands after signing in. */
export const ADMIN_LANDING_ROUTE = '/app-roles';

/**
 * Where everybody else lands: the first entry of 首頁管理 Home, the topmost group an operator
 * without the Admin role can actually open.
 */
export const DEFAULT_LANDING_ROUTE = '/featured-promo-items';

/**
 * Blocks the 系統管理 Admin routes for an operator without the role.
 *
 * **This is not the protection.** The API refuses those endpoints itself — see the Admin policy in
 * `AuthorizationPolicies` — and it would go on refusing them if this file were deleted. What this
 * adds is that the operator never arrives at a page which can only fail: the menu already hides
 * 系統管理 Admin, but a typed URL, a bookmark or the `**` fallback route would otherwise land them
 * on a list whose every request answers 403.
 *
 * Attached as `canActivateChild` on a path-less parent wrapping the admin routes, the same shape
 * `authGuard` uses on the shell: a route added under it is guarded by omission rather than left
 * open by it.
 */
export const adminGuard: CanActivateChildFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAdmin() || router.createUrlTree([DEFAULT_LANDING_ROUTE]);
};

/**
 * The route to send a signed-in operator to when they asked for nothing in particular — the empty
 * path and the `**` fallback. It has to be a function rather than a constant `redirectTo`, because
 * the answer depends on who is signed in: 角色 AppRole was the landing page for everybody until the
 * API started refusing it, which would have dropped every non-admin on a 403 the moment they
 * signed in.
 */
export const landingRedirect = (): UrlTree => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return router.parseUrl(auth.isAdmin() ? ADMIN_LANDING_ROUTE : DEFAULT_LANDING_ROUTE);
};
