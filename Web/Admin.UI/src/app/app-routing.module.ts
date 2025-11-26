import { RouterModule, Routes } from '@angular/router';

import { NgModule } from '@angular/core';
import { AuthGuard } from './services/security/auth.guard'; // adjust the path


const routes: Routes = [
  { path: 'logout', loadComponent: () => import('./components/logout/logout.component').then(mod => mod.LogOutComponent) },
  { path: 'login', loadComponent: () => import('./components/login/login.component').then(mod => mod.LoginComponent) },
  { path: 'unauthorized', loadComponent: () => import('./components/core/unauthorized/unauthorized.component').then(mod => mod.UnauthorizedComponent) },
  { path: 'dashboard', loadComponent: () => import('./components/dashboard/admin-dashboard/admin-dashboard.component').then(mod => mod.AdminDashboardComponent), canActivate: [AuthGuard]},
  { path: 'tenant', loadComponent: () => import('./components/tenant/tenant-dashboard/tenant-dashboard.component').then(mod => mod.TenantDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'tenant/facility/:id/edit', loadComponent: () => import('./components/tenant/facility-edit/facility-edit.component').then(mod => mod.FacilityEditComponent), canActivate: [AuthGuard]  },
  { path: 'tenant/facility/:facilityId', loadComponent: () => import('./components/tenant/facility-view/facility-view.component').then(mod => mod.FacilityViewComponent), canActivate: [AuthGuard]  },
  { path: 'tenant/facility/:facilityId/report/:reportId', loadComponent: () => import('./components/tenant/facility-view/view-report/view-report.component').then(mod => mod.ViewReportComponent), canActivate: [AuthGuard] },
  { path: 'tenant/operations', loadComponent: () => import('./components/tenant/global-operations/global-operations-search.component').then(mod => mod.GlobalOperationsSearchComponent), canActivate: [AuthGuard]  },
  { path: 'tenant/acquisition-log', loadComponent: () => import('./components/tenant/acquisition-log/acquisition-log-view/acquisition-log-view.component').then(mod => mod.AcquisitionLogViewComponent), canActivate: [AuthGuard]  },
  { path: 'measure-def', loadComponent: () => import('./components/measure-def/measure-def-config-form/measure-def-config-form.component').then(mod => mod.MeasureDefinitionFormComponent), canActivate: [AuthGuard]  },
  { path: 'notification', loadComponent: () => import('./components/notification/notification-dashboard/notification-dashboard.component').then(mod => mod.NotificationDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'notification-configuration', loadComponent: () => import('./components/notification/facility-configuration/notification-configuration.component').then(mod => mod.NotificationConfigurationComponent), canActivate: [AuthGuard]  },
  { path: 'audit', loadComponent: () => import('./components/audit/audit-dashboard/audit-dashboard.component').then(mod => mod.AuditDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'account', loadComponent: () => import('./components/account/account-dashboard/account-dashboard.component').then(mod => mod.AccountDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'vendor', loadComponent: () => import('./components/vendor/vendor-dashboard/vendor-dashboard.component').then(mod => mod.VendorDashboardComponent), canActivate: [AuthGuard] },
  { path: 'integration-test', loadComponent: () => import('./components/testing/integration-test/integration-test.component').then(mod => mod.IntegrationTestComponent), canActivate: [AuthGuard] },
  { path: 'validation-config', loadComponent: () => import('./components/validation-config/validation-config.component').then(mod => mod.ValidationConfigComponent), canActivate: [AuthGuard]  },
  { path: 'monitor/health', loadComponent: () => import('./components/monitor/link-health-check/link-health-check.component').then(mod => mod.LinkHealthCheckComponent), canActivate: [AuthGuard]  },
  { path: 'themes', loadComponent: () => import('./components/theme-showcase/theme-showcase.component').then(mod => mod.ThemeShowcaseComponent) , canActivate: [AuthGuard] },
  { path: 'reports', loadComponent: () => import('./components/reports/reports-dashboard/reports-dashboard.component').then(mod => mod.ReportsDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'reports/generate-report', loadComponent: () => import('./components/reports/generate-report/generate-report-form.component').then(mod => mod.GenerateReportFormComponent), canActivate: [AuthGuard]  },
  { path: 'sub-pre-qual-report/facility/:facilityId/report/:submissionId', loadComponent: () => import('./components/sub-pre-qual-report/sub-pre-qual-report.component').then(mod => mod.SubPreQualReportComponent), canActivate: [AuthGuard]  },
  { path: 'sub-pre-qual-report/facility/:facilityId/report/:submissionId/:categoryId', loadComponent: () => import('./components/sub-pre-qual-report/sub-pre-qual-report-issues/sub-pre-qual-report-issues.component').then(mod => mod.SubPreQualReportIssuesComponent), canActivate: [AuthGuard]  },
  { path: 'validation-config/validation-categories', loadComponent: () => import('./components/validation-config/validation-categories/validation-categories-list/validation-categories-list.component').then(mod => mod.ValidationCategoriesComponent) , canActivate: [AuthGuard] },
  { path: 'validation-config/validation-categories/:id/edit', loadComponent: () => import('./components/validation-config/validation-categories/edit-validation-category/edit-validation-category.component').then(mod => mod.EditValidationCategoryComponent), canActivate: [AuthGuard]  },
  { path: 'query-plans', loadComponent: () => import('./components/query-plans/query-plans-dashboard/query-plans-dashboard.component').then(mod => mod.QueryPlansDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'app-configuration', loadComponent: () => import('./components/app-configuration/app-configuration-dashboard/app-configuration-dashboard.component').then(mod => mod.AppConfigurationDashboardComponent), canActivate: [AuthGuard]  },
  { path: 'kafka', loadComponent: () => import('./components/kafka/kafka-dashboard/kafka-dashboard.component').then(mod => mod.KafkaDashboardComponent) , canActivate: [AuthGuard] },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
