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

import { environment } from '../environments/environment';
import { HttpInterceptorProviders } from './interceptors/interceptor.barrel';
import { DataAcquisitionFhirQueryConfigFormComponent } from './components/data-acquisition/data-acquisition-fhir-query-config-form/data-acquisition-fhir-query-config-form.component';
import { DataAcquisitionFhirQueryConfigDialogComponent } from './components/data-acquisition/data-acquisition-fhir-query-config-dialog/data-acquisition-fhir-query-config-dialog.component';
import { DataAcquisitionFhirListConfigDialogComponent } from './components/data-acquisition/data-acquisition-fhir-list-config-dialog/data-acquisition-fhir-list-config-dialog.component';
import { DataAcquisitionFhirListConfigFormComponent } from './components/data-acquisition/data-acquisition-fhir-list-config-form/data-acquisition-fhir-list-config-form.component';
import { DataAcquisitionAuthenticationConfigFormComponent } from './components/data-acquisition/data-acquisition-authentication-config-form/data-acquisition-authentication-config-form.component';
import { DataAcquisitionAuthenticationConfigDialogComponent } from './components/data-acquisition/data-acquisition-authentication-config-dialog/data-acquisition-authentication-config-dialog.component';
import { MatDateSelectionModel } from '@angular/material/datepicker';



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
    LoadingIndicatorComponent,
    MatSelectModule,
  ],
  providers: [
    StyleManagerService,
    HttpInterceptorProviders
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
