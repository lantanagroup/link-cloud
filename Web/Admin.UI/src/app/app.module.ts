import { APP_INITIALIZER, NgModule } from '@angular/core';
import { AuthConfig, OAuthModule, OAuthModuleConfig, OAuthService } from "angular-oauth2-oidc";
import { provideHttpClient, withInterceptorsFromDi } from "@angular/common/http";

import { AppComponent } from './app.component';
import { AppConfigService } from './services/app-config.service';
import { AppRoutingModule } from './app-routing.module';
import { AuthenticationService } from './services/security/authentication.service';
import { BreadcrumbComponent } from "./components/core/breadcrumb/breadcrumb.component";
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { BrowserModule } from '@angular/platform-browser';
import { FooterComponent } from "./components/core/footer/footer.component";
import { HttpInterceptorProviders } from './interceptors/interceptor.barrel';
import { LayoutModule } from '@angular/cdk/layout';
import { LinkAdminSubnavBarComponent } from "./components/core/link-admin-subnav-bar/link-admin-subnav-bar.component";
import { LinkNavBarComponent } from './components/core/link-nav-bar/link-nav-bar.component';
import { LoadingIndicatorComponent } from './components/core/loading-indicator/loading-indicator.component';
import { MatButtonModule } from '@angular/material/button';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { StyleManagerService } from './services/style-manager-service';
import { SubPreQualReportBannerComponent } from './components/sub-pre-qual-report/sub-pre-qual-report-banner/sub-pre-qual-report-banner.component';
import { SubPreQualReportCategoriesTableComponent } from './components/sub-pre-qual-report/sub-pre-qual-report-categories-table/sub-pre-qual-report-categories-table.component';
import { SubPreQualReportComponent } from './components/sub-pre-qual-report/sub-pre-qual-report.component';
import { SubPreQualReportMetaComponent } from './components/sub-pre-qual-report/sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import { SubPreQualReportSummaryComponent } from './components/sub-pre-qual-report/sub-pre-qual-report-summary/sub-pre-qual-report-summary.component';
import { ThemePickerComponent } from './components/core/theme-picker/theme-picker.component';
import { ToastrModule } from 'ngx-toastr';
import { VdButtonComponent } from './components/core/vd-button/vd-button.component';
import { VdIconComponent } from './components/core/vd-icon/vd-icon.component';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { TenantSearchBarComponent } from "./components/core/tenant-search-bar/tenant-search-bar/tenant-search-bar.component";

export function initConfig(appConfig: AppConfigService, oauthService: OAuthService, authService: AuthenticationService, oauthModuleConfig: OAuthModuleConfig) {
  const configPromise = appConfig.loadConfig()
    .then(async config => {
      if (config?.oauth2?.enabled) {
        const authConfig: AuthConfig = {
          issuer: config.oauth2.issuer,
          clientId: config.oauth2.clientId,
          scope: config.oauth2.scope,
          responseType: config.oauth2.responseType,
          requireHttps: config.oauth2.requireHttps ?? true,
          disablePKCE: config.oauth2.disablePKCE || false,
          skipIssuerCheck: config.oauth2.skipIssuerCheck || false,
          redirectUri: window.location.origin + '/login-oauth2'
        };

        oauthService.configure(authConfig);
        oauthService.setupAutomaticSilentRefresh();

        oauthModuleConfig.resourceServer.allowedUrls = [config.baseApiUrl];

        await oauthService.loadDiscoveryDocument();
        await oauthService.tryLogin();

        if (oauthService.hasValidAccessToken()) {
          await authService.loadProfile();
        }
      }
    });
  return () => configPromise;
}

@NgModule({
  declarations: [
    AppComponent
  ],
  bootstrap: [AppComponent],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    AppRoutingModule,
    LayoutModule,
    MatToolbarModule,
    MatButtonModule,
    MatSidenavModule,
    MatIconModule,
    MatListModule,
    ThemePickerComponent,
    MatMenuModule,
    MatExpansionModule,
    MatNativeDateModule,
    LoadingIndicatorComponent,
    LinkNavBarComponent,
    BreadcrumbComponent,
    FooterComponent,
    ToastrModule.forRoot(),
    OAuthModule.forRoot({
        resourceServer: {
            allowedUrls: [], // This will be populated during the APP_INITIALIZER
            sendAccessToken: true
        }
    }),
    VdIconComponent,
    VdButtonComponent,
    LinkAdminSubnavBarComponent,
    SubPreQualReportComponent,
    SubPreQualReportBannerComponent,
    SubPreQualReportMetaComponent,
    SubPreQualReportSummaryComponent,
    SubPreQualReportCategoriesTableComponent,
    TenantSearchBarComponent
],
  providers: [
    {
      provide: APP_INITIALIZER,
      useFactory: initConfig,
      deps: [AppConfigService, OAuthService, AuthenticationService, OAuthModuleConfig],
      multi: true,
    },
    StyleManagerService,
    HttpInterceptorProviders,
    AuthenticationService,
    provideHttpClient(withInterceptorsFromDi()),
    provideCharts(withDefaultRegisterables())
  ]
})
export class AppModule { }
