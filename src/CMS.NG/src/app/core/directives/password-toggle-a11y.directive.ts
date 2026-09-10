import { AfterViewInit, Directive, ElementRef, OnDestroy, inject } from '@angular/core';

const SHOW_LABEL = '顯示密碼';
const HIDE_LABEL = '隱藏密碼';

/**
 * Makes PrimeNG's `[toggleMask]` reveal control reachable and announceable.
 *
 * `p-password` renders the eye as a bare `<svg>` with a click handler and nothing else: no
 * `tabindex`, no `role`, no accessible name. On the Login page that left the form with exactly
 * three focusable elements — the two inputs and the submit button — so a keyboard-only operator
 * could not reveal what they had typed, and a screen reader was never told the control existed.
 *
 * A directive rather than a replacement control: the alternative is dropping `p-password` for a
 * hand-rolled input and toggle in all four places it is used, which trades a small accessibility
 * gap for a large surface of new form wiring.
 *
 * PrimeNG swaps the whole element when the mask flips (EyeIcon and EyeSlashIcon are separate
 * components), so the attributes are re-applied by a MutationObserver rather than set once.
 */
@Directive({
  selector: 'p-password[appPasswordToggleA11y]',
})
export class PasswordToggleA11yDirective implements AfterViewInit, OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
  private observer?: MutationObserver;

  ngAfterViewInit(): void {
    this.decorate();
    // The icon element is replaced on every toggle, so re-decorate whenever the subtree changes.
    this.observer = new MutationObserver(() => this.decorate());
    this.observer.observe(this.host, { childList: true, subtree: true });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private decorate(): void {
    const icon = this.host.querySelector<SVGElement>('svg');
    if (!icon) {
      return;
    }

    const masked = this.host.querySelector('input')?.getAttribute('type') === 'password';
    const label = masked ? SHOW_LABEL : HIDE_LABEL;

    // Cheap guard against the observer re-entering on attributes this method just wrote.
    if (icon.getAttribute('aria-label') === label && icon.getAttribute('role') === 'button') {
      return;
    }

    icon.setAttribute('role', 'button');
    icon.setAttribute('tabindex', '0');
    icon.setAttribute('aria-label', label);
    icon.setAttribute('aria-pressed', String(!masked));

    if (icon.dataset['a11yBound'] !== 'true') {
      icon.dataset['a11yBound'] = 'true';
      // Enter and Space are what a real <button> would answer to; the click handler PrimeNG
      // already bound does the actual toggling.
      icon.addEventListener('keydown', (event: KeyboardEvent) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          (event.currentTarget as SVGElement).dispatchEvent(
            new MouseEvent('click', { bubbles: true }),
          );
        }
      });
    }
  }
}
