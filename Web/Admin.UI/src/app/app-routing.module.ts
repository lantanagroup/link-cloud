import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: 'logout', loadComponent: () => import('./components/logout/logout.component').then(mod => mod.LogOutComponent)  },
  { path: 'dashboard', loadComponent: () => import('./components/dashboard/admin-dashboard/admin-dashboard.component').then(mod => mod.AdminDashboardComponent) },
  { path: 'logout', loadComponent: () => import('./components/dashboard/admin-dashboard/admin-dashboard.component').then(mod => mod.AdminDashboardComponent) },
  { path: 'tenant', loadComponent: () => import('./components/tenant/tenant-dashboard/tenant-dashboard.component').then(mod => mod.TenantDashboardComponent) },
  { path: 'tenant/facility/:id/edit', loadComponent: () => import('./components/tenant/facility-edit/facility-edit.component').then(mod => mod.FacilityEditComponent) },
  { path: 'tenant/facility/:facilityId', loadComponent: () => import('./components/tenant/facility-view/facility-view.component').then(mod => mod.FacilityViewComponent) },
  { path: 'tenant/facility/:facilityId/report/:reportId', loadComponent: () => import('./components/tenant/facility-view/view-report/view-report.component').then(mod => mod.ViewReportComponent) },
  { path: 'measure-def', loadComponent: () => import('./components/measure-def/measure-def-config-form/measure-def-config-form.component').then(mod => mod.MeasureDefinitionFormComponent) },
  { path: 'notification', loadComponent: () => import('./components/notification/notification-dashboard/notification-dashboard.component').then(mod => mod.NotificationDashboardComponent) },
  { path: 'notification-configuration', loadComponent: () => import('./components/notification/facility-configuration/notification-configuration.component').then(mod => mod.NotificationConfigurationComponent) },
  { path: 'audit', loadComponent: () => import('./components/audit/audit-dashboard/audit-dashboard.component').then(mod => mod.AuditDashboardComponent) },
  { path: 'account', loadComponent: () => import('./components/account/account-dashboard/account-dashboard.component').then(mod => mod.AccountDashboardComponent) },
  { path: 'integration-test', loadComponent: () => import('./components/testing/integration-test/integration-test.component').then(mod => mod.IntegrationTestComponent) },
  { path: 'validation-config', loadComponent: () => import('./components/validation-config/validation-config.component').then(mod => mod.ValidationConfigComponent) },
  { path: 'monitor/health', loadComponent: () => import('./components/monitor/link-health-check/link-health-check.component').then(mod => mod.LinkHealthCheckComponent) },
  { path: 'themes', loadComponent: () => import('./components/theme-showcase/theme-showcase.component').then(mod => mod.ThemeShowcaseComponent) },
  { path: 'reports/generate-report', loadComponent: () => import('./components/reports/generate-report/generate-report-form.component').then(mod => mod.GenerateReportFormComponent) },
  { path: 'unauthorized', loadComponent: () => import('./components/core/unauthorized/unauthorized.component').then(mod => mod.UnauthorizedComponent) },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
