import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', loadComponent: () => import('./components/dashboard/admin-dashboard/admin-dashboard.component').then(mod => mod.AdminDashboardComponent) },
  { path: 'tenant', loadComponent: () => import('./components/tenant/tenant-dashboard/tenant-dashboard.component').then(mod => mod.TenantDashboardComponent) },
  { path: 'notification', loadComponent: () => import('./components/notification/notification-dashboard/notification-dashboard.component').then(mod => mod.NotificationDashboardComponent) },
  { path: 'notification-configuration', loadComponent: () => import('./components/notification/facility-configuration/notification-configuration.component').then(mod => mod.NotificationConfigurationComponent) },
  { path: 'audit', loadComponent: () => import('./components/audit/audit-dashboard/audit-dashboard.component').then(mod => mod.AuditDashboardComponent) },
  { path: 'account', loadComponent: () => import('./components/account/account-dashboard/account-dashboard.component').then(mod => mod.AccountDashboardComponent) },
  { path: 'integration-test', loadComponent: () => import('./components/testing/integration-test/integration-test.component').then(mod => mod.IntegrationTestComponent) },
  { path: 'themes', loadComponent: () => import('./components/theme-showcase/theme-showcase.component').then(mod => mod.ThemeShowcaseComponent) },
  { path: 'unauthorized', loadComponent: () => import('./components/core/unauthorized/unauthorized.component').then(mod => mod.UnauthorizedComponent) },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
