import { NgModule } from '@angular/core';
import { OAuthModule } from 'angular-oauth2-oidc';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule } from "@angular/common/http";

import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AppRoutingModule } from './app-routing.module';

import { LayoutModule } from '@angular/cdk/layout';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ThemePickerComponent } from './components/core/theme-picker/theme-picker.component';
import { StyleManagerService } from './services/style-manager-service';
import { MatMenuModule } from '@angular/material/menu';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';

import { LoadingIndicatorComponent } from './components/core/loading-indicator/loading-indicator.component';

import { HttpInterceptorProviders } from './interceptors/interceptor.barrel';
import { APP_INITIALIZER } from '@angular/core';
import { AppConfigService } from './services/app-config.service';
import { AuthenticationService } from './services/security/authentication.service';
import {MatSnackBarModule} from "@angular/material/snack-bar";

export function initConfig(appConfig: AppConfigService) {
  return () => appConfig.loadConfig();
}

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
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
    MatSelectModule,
    MatSnackBarModule
  ],
  providers: [
    {
      provide: APP_INITIALIZER,
      useFactory: initConfig,
      deps: [AppConfigService],
      multi: true,
    },
    StyleManagerService,
    HttpInterceptorProviders,
    AuthenticationService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
