import { Routes } from '@angular/router';

import { adminGuard, landingRedirect } from '@core/guards/admin.guard';
import { authGuard } from '@core/guards/auth.guard';

export const routes: Routes = [
  // The only public route. It sits outside the guarded shell so a signed-out visitor can reach it.
  {
    path: 'login',
    title: '登入 Login',
    loadComponent: () => import('@features/auth/login/login').then((m) => m.Login),
  },

  // Everything else lives behind the guard. canActivateChild on the parent rather than a
  // canActivate per route, so a route added later is protected by default.
  {
    path: '',
    canActivateChild: [authGuard],
    children: [
      // Where the operator lands having asked for nothing in particular. A function rather than a
      // constant, because 角色 AppRole is 系統管理 Admin and the API refuses it to everybody else.
      { path: '', pathMatch: 'full', redirectTo: landingRedirect },
      {
        // 個人資料 — reachable by every signed-in operator, so it carries no role gate.
        path: 'profile',
        title: '個人資料 My Profile',
        loadComponent: () => import('@features/auth/profile/profile').then((m) => m.Profile),
      },

      // 系統管理 Admin. Grouped under a path-less parent so the role check is written once, the
      // same shape authGuard uses on the shell above — a route added here is guarded by omission.
      // The API refuses these endpoints on its own; this only keeps an operator off a page that
      // could do nothing but fail.
      {
        path: '',
        canActivateChild: [adminGuard],
        children: [
          {
            path: 'app-roles',
            title: '角色 AppRole',
            loadComponent: () =>
              import('@features/app-roles/app-role-list/app-role-list').then((m) => m.AppRoleList),
          },
          {
            path: 'app-roles/new',
            title: '新增角色',
            loadComponent: () =>
              import('@features/app-roles/app-role-form/app-role-form').then((m) => m.AppRoleForm),
          },
          {
            path: 'app-roles/:id',
            title: '檢視角色',
            loadComponent: () =>
              import('@features/app-roles/app-role-detail/app-role-detail').then(
                (m) => m.AppRoleDetail,
              ),
          },
          {
            path: 'app-roles/:id/edit',
            title: '編輯角色',
            loadComponent: () =>
              import('@features/app-roles/app-role-form/app-role-form').then((m) => m.AppRoleForm),
          },
          {
            path: 'publish-statuses',
            title: '發布狀態 PublishStatus',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-list/publish-status-list').then(
                (m) => m.PublishStatusList,
              ),
          },
          {
            path: 'publish-statuses/new',
            title: '新增發布狀態',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-form/publish-status-form').then(
                (m) => m.PublishStatusForm,
              ),
          },
          {
            path: 'publish-statuses/:id',
            title: '檢視發布狀態',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-detail/publish-status-detail').then(
                (m) => m.PublishStatusDetail,
              ),
          },
          {
            path: 'publish-statuses/:id/edit',
            title: '編輯發布狀態',
            loadComponent: () =>
              import('@features/publish-statuses/publish-status-form/publish-status-form').then(
                (m) => m.PublishStatusForm,
              ),
          },
          {
            path: 'app-users',
            title: '使用者 AppUser',
            loadComponent: () =>
              import('@features/app-users/app-user-list/app-user-list').then((m) => m.AppUserList),
          },
          {
            path: 'app-users/new',
            title: '新增使用者',
            loadComponent: () =>
              import('@features/app-users/app-user-form/app-user-form').then((m) => m.AppUserForm),
          },
          {
            path: 'app-users/:id',
            title: '檢視使用者',
            loadComponent: () =>
              import('@features/app-users/app-user-detail/app-user-detail').then(
                (m) => m.AppUserDetail,
              ),
          },
          {
            path: 'app-users/:id/edit',
            title: '編輯使用者',
            loadComponent: () =>
              import('@features/app-users/app-user-form/app-user-form').then((m) => m.AppUserForm),
          },
        ],
      },

      {
        path: 'partners',
        title: '原廠 Partner',
        loadComponent: () =>
          import('@features/partners/partner-list/partner-list').then((m) => m.PartnerList),
      },
      {
        path: 'partners/new',
        title: '新增原廠',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form').then((m) => m.PartnerForm),
      },
      {
        path: 'partners/:id',
        title: '檢視原廠',
        loadComponent: () =>
          import('@features/partners/partner-detail/partner-detail').then((m) => m.PartnerDetail),
      },
      {
        path: 'partners/:id/edit',
        title: '編輯原廠',
        loadComponent: () =>
          import('@features/partners/partner-form/partner-form').then((m) => m.PartnerForm),
      },
      {
        path: 'course-groups',
        title: '課程群組 CourseGroup',
        loadComponent: () =>
          import('@features/course-groups/course-group-list/course-group-list').then(
            (m) => m.CourseGroupList,
          ),
      },
      {
        path: 'course-groups/new',
        title: '新增課程群組',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form').then(
            (m) => m.CourseGroupForm,
          ),
      },
      {
        path: 'course-groups/:id',
        title: '檢視課程群組',
        loadComponent: () =>
          import('@features/course-groups/course-group-detail/course-group-detail').then(
            (m) => m.CourseGroupDetail,
          ),
      },
      {
        path: 'course-groups/:id/edit',
        title: '編輯課程群組',
        loadComponent: () =>
          import('@features/course-groups/course-group-form/course-group-form').then(
            (m) => m.CourseGroupForm,
          ),
      },
      {
        path: 'courses',
        title: '課程 Course',
        loadComponent: () =>
          import('@features/courses/course-list/course-list').then((m) => m.CourseList),
      },
      {
        path: 'courses/new',
        title: '新增課程',
        loadComponent: () =>
          import('@features/courses/course-form/course-form').then((m) => m.CourseForm),
      },
      {
        path: 'courses/:id',
        title: '檢視課程',
        loadComponent: () =>
          import('@features/courses/course-detail/course-detail').then((m) => m.CourseDetail),
      },
      {
        path: 'courses/:id/edit',
        title: '編輯課程',
        loadComponent: () =>
          import('@features/courses/course-form/course-form').then((m) => m.CourseForm),
      },
      {
        path: 'featured-promo-items',
        title: '上稿作業 FeaturedPromoItem',
        loadComponent: () =>
          import('@features/featured-promo-items/featured-promo-item-list/featured-promo-item-list').then(
            (m) => m.FeaturedPromoItemList,
          ),
      },
      { path: '**', redirectTo: landingRedirect },
    ],
  },
];
