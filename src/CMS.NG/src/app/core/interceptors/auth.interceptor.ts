import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';

import { environment } from '@env';
import { AuthService } from '@core/services/auth.service';
import { LOGIN_ROUTE } from '@core/guards/auth.guard';

/** The one API call that is expected to answer 401 to a signed-out caller. */
const LOGIN_URL = `${environment.apiUrl}/auth/login`;

/** Fixed summary for a server-side failure. The detail is whatever the API called it. */
export const SERVER_ERROR_SUMMARY = '系統錯誤';

/**
 * Shown when the response carried no message of its own — a 502 from a proxy, or a 500 from
 * something that answered before the API's exception middleware could.
 */
export const SERVER_ERROR_FALLBACK = '系統發生錯誤，請稍後再試。';

/** Fixed summary for a request the API understood and refused on the caller's authority. */
export const FORBIDDEN_SUMMARY = '權限不足';

/** Shown when a 403 carried no message of its own. */
export const FORBIDDEN_FALLBACK = '您沒有執行這項操作的權限。';

/**
 * Attaches the stored bearer token to every API request, and turns what comes back on a failure
 * into the one thing the operator should see.
 *
 * Only requests to `environment.apiUrl` are touched: the token belongs to this API and has no
 * business being sent to an asset host or a third party.
 *
 * Two failures are handled here rather than by each caller, because neither is about the record
 * the caller was working on:
 *
 * - **401 — the session is over.** Clear it and return to 登入.
 * - **5xx — the server broke.** Toast the safe message the API's exception middleware sent, which
 *   is the only description of the failure that exists client-side: the stack trace, the statement
 *   and the connection details all stayed on the server, by design. A page cannot say anything
 *   truer about it than the API already did, so this is the one place that reports it.
 *
 * Everything else — a 400 the form renders under its fields, a 404, a 409 the page explains in its
 * own words — is passed on untouched for the caller to handle, exactly as before.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const messageService = inject(MessageService);

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

      // 403 — the session is fine, this operator may not do this. Unlike a 401 there is nothing to
      // clear and nowhere to send them; the API's own wording says which rule refused, so it is
      // shown rather than swallowed. Reported here for the same reason the 5xx is: no page can say
      // anything truer about it, and a page that stayed silent would look broken instead.
      if (isApiRequest && isForbidden(error)) {
        messageService.add({
          severity: 'warn',
          summary: FORBIDDEN_SUMMARY,
          detail: safeMessage(error, FORBIDDEN_FALLBACK),
        });
      }

      if (isApiRequest && isServerError(error)) {
        messageService.add({
          severity: 'error',
          summary: SERVER_ERROR_SUMMARY,
          detail: safeMessage(error),
        });
      }

      return throwError(() => error);
    }),
  );
};

function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}

/**
 * A failure the server owns. `status` 0 is deliberately not one of these: the request never got an
 * answer, so there is no server message to show and nothing changed about the previous behaviour.
 */
function isServerError(error: unknown): error is HttpErrorResponse {
  return error instanceof HttpErrorResponse && error.status >= 500;
}

function isForbidden(error: unknown): error is HttpErrorResponse {
  return error instanceof HttpErrorResponse && error.status === 403;
}

/**
 * The message out of the ProblemDetails body, preferring `title` — the Chinese wording, which is
 * what the operator reads — over `detail`, the English sentence beside it. Anything that is not a
 * non-empty string falls back: a 5xx from outside the API answers with an HTML error page, and a
 * fragment of it is worse than saying nothing specific.
 */
function safeMessage(error: HttpErrorResponse, fallback = SERVER_ERROR_FALLBACK): string {
  const body: unknown = error.error;

  if (body && typeof body === 'object') {
    const problem = body as { title?: unknown; detail?: unknown };
    return text(problem.title) ?? text(problem.detail) ?? fallback;
  }

  return fallback;
}

function text(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null;
}
