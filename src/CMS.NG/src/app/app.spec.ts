import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the brand title', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.brand-text').textContent).toContain('UWA');
  });

  it('renders the 系統管理 Admin group with the 角色 AppRole item', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');

    const link = fixture.nativeElement.querySelector('a.nav-item');
    expect(link.getAttribute('href')).toBe('/app-roles');
  });

  it('renders the 發布狀態 PublishStatus item under 系統管理 Admin', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('發布狀態 PublishStatus');

    const hrefs = Array.from(
      fixture.nativeElement.querySelectorAll('a.nav-item') as NodeListOf<HTMLAnchorElement>,
    ).map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(['/app-roles', '/publish-statuses']);
  });

  it('renders the 課程管理 Course items once the group is expanded', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
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

    const hrefs = Array.from(
      fixture.nativeElement.querySelectorAll('a.nav-item') as NodeListOf<HTMLAnchorElement>,
    ).map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual([
      '/partners',
      '/course-groups',
      '/courses',
      '/app-roles',
      '/publish-statuses',
    ]);
  });

  it('collapses and expands a nav group', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    expect(component['isExpanded']('系統管理 Admin')).toBeTrue();

    component['toggleGroup']('系統管理 Admin');
    fixture.detectChanges();

    expect(component['isExpanded']('系統管理 Admin')).toBeFalse();
    expect(fixture.nativeElement.querySelector('a.nav-item')).toBeNull();
  });

  it('toggles the sidebar', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as Record<string, any>;

    component['toggleSidebar']();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.app-shell').classList).toContain('collapsed');
  });
});
