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

import { LoadingIndicatorComponent } from './components/core/loading-indicator/loading-indicator.component';

import { environment } from '../environments/environment';
import { HttpInterceptorProviders } from './interceptors/interceptor.barrel';


@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    OAuthModule.forRoot({
      resourceServer: {
        allowedUrls: [`${environment.baseApiUrl}/api`],
        sendAccessToken: true
      }
    }),
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
    LoadingIndicatorComponent
  ],
  providers: [
    StyleManagerService,
    HttpInterceptorProviders
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
