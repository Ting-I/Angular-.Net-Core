import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app-roles' },
  {
    path: 'app-roles',
    loadComponent: () =>
      import('@features/app-roles/app-role-list/app-role-list').then((m) => m.AppRoleList),
  },
  {
    path: 'app-roles/new',
    loadComponent: () =>
      import('@features/app-roles/app-role-form/app-role-form').then((m) => m.AppRoleForm),
  },
  {
    path: 'app-roles/:id',
    loadComponent: () =>
      import('@features/app-roles/app-role-detail/app-role-detail').then((m) => m.AppRoleDetail),
  },
  {
    path: 'app-roles/:id/edit',
    loadComponent: () =>
      import('@features/app-roles/app-role-form/app-role-form').then((m) => m.AppRoleForm),
  },
  {
    path: 'publish-statuses',
    loadComponent: () =>
      import('@features/publish-statuses/publish-status-list/publish-status-list').then(
        (m) => m.PublishStatusList,
      ),
  },
  {
    path: 'publish-statuses/new',
    loadComponent: () =>
      import('@features/publish-statuses/publish-status-form/publish-status-form').then(
        (m) => m.PublishStatusForm,
      ),
  },
  {
    path: 'publish-statuses/:id',
    loadComponent: () =>
      import('@features/publish-statuses/publish-status-detail/publish-status-detail').then(
        (m) => m.PublishStatusDetail,
      ),
  },
  {
    path: 'publish-statuses/:id/edit',
    loadComponent: () =>
      import('@features/publish-statuses/publish-status-form/publish-status-form').then(
        (m) => m.PublishStatusForm,
      ),
  },
  {
    path: 'partners',
    loadComponent: () =>
      import('@features/partners/partner-list/partner-list').then((m) => m.PartnerList),
  },
  {
    path: 'partners/new',
    loadComponent: () =>
      import('@features/partners/partner-form/partner-form').then((m) => m.PartnerForm),
  },
  {
    path: 'partners/:id',
    loadComponent: () =>
      import('@features/partners/partner-detail/partner-detail').then((m) => m.PartnerDetail),
  },
  {
    path: 'partners/:id/edit',
    loadComponent: () =>
      import('@features/partners/partner-form/partner-form').then((m) => m.PartnerForm),
  },
  {
    path: 'course-groups',
    loadComponent: () =>
      import('@features/course-groups/course-group-list/course-group-list').then(
        (m) => m.CourseGroupList,
      ),
  },
  {
    path: 'course-groups/new',
    loadComponent: () =>
      import('@features/course-groups/course-group-form/course-group-form').then(
        (m) => m.CourseGroupForm,
      ),
  },
  {
    path: 'course-groups/:id',
    loadComponent: () =>
      import('@features/course-groups/course-group-detail/course-group-detail').then(
        (m) => m.CourseGroupDetail,
      ),
  },
  {
    path: 'course-groups/:id/edit',
    loadComponent: () =>
      import('@features/course-groups/course-group-form/course-group-form').then(
        (m) => m.CourseGroupForm,
      ),
  },
  {
    path: 'courses',
    loadComponent: () =>
      import('@features/courses/course-list/course-list').then((m) => m.CourseList),
  },
  {
    path: 'courses/new',
    loadComponent: () =>
      import('@features/courses/course-form/course-form').then((m) => m.CourseForm),
  },
  {
    path: 'courses/:id',
    loadComponent: () =>
      import('@features/courses/course-detail/course-detail').then((m) => m.CourseDetail),
  },
  {
    path: 'courses/:id/edit',
    loadComponent: () =>
      import('@features/courses/course-form/course-form').then((m) => m.CourseForm),
  },
  { path: '**', redirectTo: 'app-roles' },
];
