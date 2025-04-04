import {APP_INITIALIZER, NgModule} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';
import {provideHttpClient, withInterceptorsFromDi} from "@angular/common/http";
import {AppComponent} from './app.component';
import {BrowserAnimationsModule} from '@angular/platform-browser/animations';
import {AppRoutingModule} from './app-routing.module';
import {LayoutModule} from '@angular/cdk/layout';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatButtonModule} from '@angular/material/button';
import {MatSidenavModule} from '@angular/material/sidenav';
import {MatIconModule} from '@angular/material/icon';
import {MatListModule} from '@angular/material/list';
import {ThemePickerComponent} from './components/core/theme-picker/theme-picker.component';
import {StyleManagerService} from './services/style-manager-service';
import {MatMenuModule} from '@angular/material/menu';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatNativeDateModule} from '@angular/material/core';
import {LoadingIndicatorComponent} from './components/core/loading-indicator/loading-indicator.component';
import {HttpInterceptorProviders} from './interceptors/interceptor.barrel';
import {AppConfigService} from './services/app-config.service';
import {AuthenticationService} from './services/security/authentication.service';
import {LinkNavBarComponent} from './components/core/link-nav-bar/link-nav-bar.component';
import {BreadcrumbComponent} from "./components/core/breadcrumb/breadcrumb.component";
import {FooterComponent} from "./components/core/footer/footer.component";
import {ToastrModule} from 'ngx-toastr';
import {AuthConfig, OAuthModule, OAuthModuleConfig, OAuthService} from "angular-oauth2-oidc";

export function initConfig(appConfig: AppConfigService, oauthService:OAuthService, authService: AuthenticationService, oauthModuleConfig: OAuthModuleConfig) {
  const configPromise = appConfig.loadConfig()
    .then(async config => {
      if (config?.oauth2?.enabled) {
        const authConfig: AuthConfig = {
          issuer: config.oauth2.issuer,
          clientId: config.oauth2.clientId,
          scope: config.oauth2.scope,
          responseType: config.oauth2.responseType,
          requireHttps: config.oauth2.requireHttps || true,
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

@NgModule({ declarations: [
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
        allowedUrls: [],      // This will be populated during the APP_INITIALIZER
        sendAccessToken: true
      }
    })
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
    provideHttpClient(withInterceptorsFromDi())
  ]
})
export class AppModule { }
