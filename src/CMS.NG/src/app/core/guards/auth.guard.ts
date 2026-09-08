import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';

import { AuthService } from '@core/services/auth.service';

/** Where an unauthenticated visitor lands. The only public route in the app. */
export const LOGIN_ROUTE = '/login';

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
