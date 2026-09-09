import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';

import { environment } from '@env';
import { App } from './app';
import { AuthService } from '@core/services/auth.service';
import { fakeProfile } from '@core/testing/fake-jwt';

describe('App', () => {
  /**
   * The shell only renders for a signed-in operator, so every spec seeds a session first — and
   * the roles it seeds decide which nav groups exist at all.
   */
  function signIn(...roles: string[]): void {
    sessionStorage.setItem(
      'auth-profile',
      JSON.stringify(fakeProfile('helen', 'Helen Lin', roles)),
    );
  }

  function createApp() {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        // The shell renders the root <p-toast /> authInterceptor's 5xx message goes to.
        MessageService,
      ],
    });

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  const navHrefs = (fixture: { nativeElement: HTMLElement }) =>
    Array.from(fixture.nativeElement.querySelectorAll('a.nav-item')).map((a) =>
      a.getAttribute('href'),
    );

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('should create the app', () => {
    signIn('Admin');
    expect(createApp().componentInstance).toBeTruthy();
  });

  it('renders the brand title', () => {
    signIn('Admin');
    const fixture = createApp();

    expect(fixture.nativeElement.querySelector('.brand-text').textContent).toContain('UWA');
  });

  it('renders the 系統管理 Admin group with the 角色 AppRole item', () => {
    signIn('Admin');
    const fixture = createApp();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');

    const link = fixture.nativeElement.querySelector('a.nav-item');
    expect(link.getAttribute('href')).toBe('/app-roles');
  });

  it('renders the 發布狀態 PublishStatus and 使用者 AppUser items under 系統管理 Admin', () => {
    signIn('Admin');
    const fixture = createApp();

    expect(fixture.nativeElement.textContent).toContain('發布狀態 PublishStatus');
    expect(fixture.nativeElement.textContent).toContain('使用者 AppUser');
    expect(navHrefs(fixture)).toEqual(['/app-roles', '/publish-statuses', '/app-users']);
  });

  it('renders the 課程管理 Course items once the group is expanded', () => {
    signIn('Admin');
    const fixture = createApp();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    // Only 系統管理 Admin starts expanded, so the Course group's items are hidden until it opens.
    expect(fixture.nativeElement.textContent).not.toContain('原廠 Partner');
    expect(fixture.nativeElement.textContent).not.toContain('課程群組 CourseGroup');
    expect(fixture.nativeElement.textContent).not.toContain('課程 Course');

    component['toggleGroup']('課程管理 Course');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('原廠 Partner');
    expect(fixture.nativeElement.textContent).toContain('課程群組 CourseGroup');
    expect(fixture.nativeElement.textContent).toContain('課程 Course');

    expect(navHrefs(fixture)).toEqual([
      '/partners',
      '/course-groups',
      '/courses',
      '/app-roles',
      '/publish-statuses',
      '/app-users',
    ]);
  });

  it('renders the 上稿作業 FeaturedPromoItem item once 首頁管理 Home is expanded', () => {
    signIn('Admin');
    const fixture = createApp();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    expect(fixture.nativeElement.textContent).not.toContain('上稿作業 FeaturedPromoItem');

    component['toggleGroup']('首頁管理 Home');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('上稿作業 FeaturedPromoItem');
    expect(navHrefs(fixture)).toEqual([
      '/featured-promo-items',
      '/app-roles',
      '/publish-statuses',
      '/app-users',
    ]);
  });

  it('collapses and expands a nav group', () => {
    signIn('Admin');
    const fixture = createApp();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    expect(component['isExpanded']('系統管理 Admin')).toBeTrue();

    component['toggleGroup']('系統管理 Admin');
    fixture.detectChanges();

    expect(component['isExpanded']('系統管理 Admin')).toBeFalse();
    expect(fixture.nativeElement.querySelector('a.nav-item')).toBeNull();
  });

  it('toggles the sidebar', () => {
    signIn('Admin');
    const fixture = createApp();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    component['toggleSidebar']();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-shell').classList).toContain('collapsed');
  });

  // ---------- 系統管理 Admin is role-gated ----------

  it('hides 系統管理 Admin from a user whose roles do not include Admin', () => {
    signIn('Editor');
    const fixture = createApp();

    expect(fixture.nativeElement.textContent).not.toContain('系統管理 Admin');
    expect(navHrefs(fixture)).toEqual([]);
  });

  it('hides 系統管理 Admin from a user with no roles at all', () => {
    signIn();
    const fixture = createApp();

    expect(fixture.nativeElement.textContent).not.toContain('系統管理 Admin');
  });

  it('keeps the other groups visible for a non-Admin user', () => {
    signIn('Editor');
    const fixture = createApp();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    expect(fixture.nativeElement.textContent).toContain('課程管理 Course');

    component['toggleGroup']('課程管理 Course');
    fixture.detectChanges();

    expect(navHrefs(fixture)).toEqual(['/partners', '/course-groups', '/courses']);
  });

  it('shows 系統管理 Admin when Admin is one of several roles', () => {
    signIn('Editor', 'Admin');
    const fixture = createApp();

    expect(fixture.nativeElement.textContent).toContain('系統管理 Admin');
    expect(navHrefs(fixture)).toEqual(['/app-roles', '/publish-statuses', '/app-users']);
  });

  // ---------- Signed-in identity and logout ----------

  it('shows the signed-in 使用者名稱 in the top bar', () => {
    signIn('Admin');
    const fixture = createApp();

    expect(fixture.nativeElement.querySelector('.topbar-user-name').textContent).toContain(
      'Helen Lin',
    );
  });

  it('links to 個人資料 My Profile from the top bar', () => {
    signIn('Admin');
    const fixture = createApp();

    const link = fixture.nativeElement.querySelector('[data-testid="profile-link"]');
    expect(link.getAttribute('href')).toBe('/profile');
    expect(link.textContent).toContain('個人資料 My Profile');
  });

  it('offers 個人資料 to an operator with no roles at all', () => {
    // Not a role-gated page: every signed-in operator has a profile of their own.
    signIn();
    const fixture = createApp();

    expect(fixture.nativeElement.querySelector('[data-testid="profile-link"]')).not.toBeNull();
  });

  it('shows the new 使用者名稱 after the profile page saves one', () => {
    signIn('Admin');
    const fixture = createApp();
    const auth = TestBed.inject(AuthService);
    const httpMock = TestBed.inject(HttpTestingController);

    // The save the Profile page makes; the shell binds to the same AuthService signal.
    auth.updateProfile({ userName: 'Helen Chen' }).subscribe();
    httpMock
      .expectOne(`${environment.apiUrl}/auth/profile`)
      .flush({ userId: 'helen', userName: 'Helen Chen', roleIds: ['Admin'] });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.topbar-user-name').textContent).toContain(
      'Helen Chen',
    );

    // The Admin group is still there — the roles come from the token, which the save left alone.
    expect(fixture.nativeElement.textContent).toContain('系統管理 Admin');
    httpMock.verify();
  });

  it('clears session storage and returns to the Login page on logout', () => {
    signIn('Admin');
    const fixture = createApp();
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    (fixture.componentInstance as unknown as Record<string, any>)['logout']();
    fixture.detectChanges();

    expect(sessionStorage.getItem('auth-profile')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);

    // The shell goes with the session; the Login page owns the viewport from here.
    expect(fixture.nativeElement.querySelector('.app-shell')).toBeNull();
  });

  it('renders no shell at all when nobody is signed in', () => {
    const fixture = createApp();

    expect(fixture.nativeElement.querySelector('.app-shell')).toBeNull();
    expect(fixture.nativeElement.querySelector('.app-sidebar')).toBeNull();
    expect(fixture.nativeElement.querySelector('.app-topbar')).toBeNull();
  });
});
