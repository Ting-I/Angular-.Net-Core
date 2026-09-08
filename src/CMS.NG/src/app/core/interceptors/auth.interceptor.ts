import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { environment } from '@env';
import { AuthService } from '@core/services/auth.service';
import { LOGIN_ROUTE } from '@core/guards/auth.guard';

/** The one API call that is expected to answer 401 to a signed-out caller. */
const LOGIN_URL = `${environment.apiUrl}/auth/login`;

/**
 * Attaches the stored bearer token to every API request, and treats a 401 coming back as the end
 * of the session.
 *
 * Only requests to `environment.apiUrl` are touched: the token belongs to this API and has no
 * business being sent to an asset host or a third party.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isApiRequest = request.url.startsWith(environment.apiUrl);
  const accessToken = auth.accessToken();

  const authorized =
    isApiRequest && accessToken
      ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      // The login call's own 401 is 帳號或密碼錯誤, which the Login page reports itself — bouncing
      // to the page the operator is already on would just wipe the message.
      if (isApiRequest && request.url !== LOGIN_URL && isUnauthorized(error)) {
        auth.clearSession();
        void router.navigate([LOGIN_ROUTE]);
      }

      return throwError(() => error);
    }),
  );
};

function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}
