import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** The product name, as the operator sees it on the sign-in card and in the sidebar. */
const APP_NAME = 'UWA';

/**
 * Puts the product name and the current page in the browser tab.
 *
 * Before this, every page in the CMS reported `CMSNG` — the Angular CLI project name, which is an
 * internal codename the operator has no reason to recognise. An operator working with several
 * records open could not tell the tabs apart, and the one place the app identifies itself when it
 * is not the active tab named the wrong thing.
 *
 * Routes carry the same bilingual label their `<h1>` uses, so the tab and the page agree.
 * A route without a title falls back to the product name alone rather than leaving whatever the
 * previous page set.
 */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `${APP_NAME} | ${page}` : APP_NAME);
  }
}
