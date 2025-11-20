import { RouterModule, Routes } from '@angular/router';

import { NgModule } from '@angular/core';

const routes: Routes = [
  { path: 'logout', loadComponent: () => import('./components/logout/logout.component').then(mod => mod.LogOutComponent) },
  { path: 'login', loadComponent: () => import('./components/login/login.component').then(mod => mod.LoginComponent) },
  { path: 'dashboard', loadComponent: () => import('./components/dashboard/admin-dashboard/admin-dashboard.component').then(mod => mod.AdminDashboardComponent) },
  { path: 'tenant', loadComponent: () => import('./components/tenant/tenant-dashboard/tenant-dashboard.component').then(mod => mod.TenantDashboardComponent) },
  { path: 'tenant/facility/:id/edit', loadComponent: () => import('./components/tenant/facility-edit/facility-edit.component').then(mod => mod.FacilityEditComponent) },
  { path: 'tenant/facility/:facilityId', loadComponent: () => import('./components/tenant/facility-view/facility-view.component').then(mod => mod.FacilityViewComponent) },
  { path: 'tenant/facility/:facilityId/report/:reportId', loadComponent: () => import('./components/tenant/facility-view/view-report/view-report.component').then(mod => mod.ViewReportComponent) },
  { path: 'tenant/operations', loadComponent: () => import('./components/tenant/global-operations/global-operations-search.component').then(mod => mod.GlobalOperationsSearchComponent) },
  { path: 'tenant/acquisition-log', loadComponent: () => import('./components/tenant/acquisition-log/acquisition-log-view/acquisition-log-view.component').then(mod => mod.AcquisitionLogViewComponent) },
  { path: 'measure-def', loadComponent: () => import('./components/measure-def/measure-def-config-form/measure-def-config-form.component').then(mod => mod.MeasureDefinitionFormComponent) },
  { path: 'notification', loadComponent: () => import('./components/notification/notification-dashboard/notification-dashboard.component').then(mod => mod.NotificationDashboardComponent) },
  { path: 'notification-configuration', loadComponent: () => import('./components/notification/facility-configuration/notification-configuration.component').then(mod => mod.NotificationConfigurationComponent) },
  { path: 'audit', loadComponent: () => import('./components/audit/audit-dashboard/audit-dashboard.component').then(mod => mod.AuditDashboardComponent) },
  { path: 'account', loadComponent: () => import('./components/account/account-dashboard/account-dashboard.component').then(mod => mod.AccountDashboardComponent) },
  { path: 'vendor', loadComponent: () => import('./components/vendor/vendor-dashboard/vendor-dashboard.component').then(mod => mod.VendorDashboardComponent) },
  { path: 'integration-test', loadComponent: () => import('./components/testing/integration-test/integration-test.component').then(mod => mod.IntegrationTestComponent) },
  { path: 'validation-config', loadComponent: () => import('./components/validation-config/validation-config.component').then(mod => mod.ValidationConfigComponent) },
  { path: 'monitor/health', loadComponent: () => import('./components/monitor/link-health-check/link-health-check.component').then(mod => mod.LinkHealthCheckComponent) },
  { path: 'themes', loadComponent: () => import('./components/theme-showcase/theme-showcase.component').then(mod => mod.ThemeShowcaseComponent) },
  { path: 'reports', loadComponent: () => import('./components/reports/reports-dashboard/reports-dashboard.component').then(mod => mod.ReportsDashboardComponent) },
  { path: 'reports/generate-report', loadComponent: () => import('./components/reports/generate-report/generate-report-form.component').then(mod => mod.GenerateReportFormComponent) },
  { path: 'unauthorized', loadComponent: () => import('./components/core/unauthorized/unauthorized.component').then(mod => mod.UnauthorizedComponent) },
  { path: 'sub-pre-qual-report/facility/:facilityId/report/:submissionId', loadComponent: () => import('./components/sub-pre-qual-report/sub-pre-qual-report.component').then(mod => mod.SubPreQualReportComponent) },
  { path: 'sub-pre-qual-report/facility/:facilityId/report/:submissionId/:categoryId', loadComponent: () => import('./components/sub-pre-qual-report/sub-pre-qual-report-issues/sub-pre-qual-report-issues.component').then(mod => mod.SubPreQualReportIssuesComponent) },
  { path: 'validation-config/validation-categories', loadComponent: () => import('./components/validation-config/validation-categories/validation-categories-list/validation-categories-list.component').then(mod => mod.ValidationCategoriesComponent) },
  { path: 'validation-config/validation-categories/:id/edit', loadComponent: () => import('./components/validation-config/validation-categories/edit-validation-category/edit-validation-category.component').then(mod => mod.EditValidationCategoryComponent) },
  { path: 'query-plans', loadComponent: () => import('./components/query-plans/query-plans-dashboard/query-plans-dashboard.component').then(mod => mod.QueryPlansDashboardComponent) },
  { path: 'app-configuration', loadComponent: () => import('./components/app-configuration/app-configuration-dashboard/app-configuration-dashboard.component').then(mod => mod.AppConfigurationDashboardComponent) },
  { path: 'kafka', loadComponent: () => import('./components/kafka/kafka-dashboard/kafka-dashboard.component').then(mod => mod.KafkaDashboardComponent) },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
