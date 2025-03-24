import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { provideHttpClient, withInterceptorsFromDi } from "@angular/common/http";

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

import { LoadingIndicatorComponent } from './components/core/loading-indicator/loading-indicator.component';

import { HttpInterceptorProviders } from './interceptors/interceptor.barrel';
import { APP_INITIALIZER } from '@angular/core';
import { AppConfigService } from './services/app-config.service';
import { AuthenticationService } from './services/security/authentication.service';
import { LinkNavBarComponent } from './components/core/link-nav-bar/link-nav-bar.component';


export function initConfig(appConfig: AppConfigService) {
  return () => appConfig.loadConfig();
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
    LinkNavBarComponent
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
    AuthenticationService,
    provideHttpClient(withInterceptorsFromDi())
  ] 
})
export class AppModule { }
