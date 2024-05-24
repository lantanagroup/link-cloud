import { HttpClient } from "@angular/common/http";
import { AuthConfig } from 'angular-oauth2-oidc';
import { Injectable } from "@angular/core";

export interface AppConfig {
  baseApiUrl: string;
  idpIssuer: string;
  idpClientId: string;
  idpClientSecret: string;
  idpScope: string;
  redirectUri: string;
  loginUrl: string
}

@Injectable({
  providedIn: 'root'
})
export class AppConfigService {
  public config?: AppConfig;

  loaded = false;

  constructor(private http: HttpClient) { }

  async loadConfig(): Promise<void> {
    try {
      this.config = await this.http.get<AppConfig>('/assets/app.config.json').toPromise();
    } catch (ex: any) {
      throw new Error('Failed to acquire app configuration: ' + (ex.message || ex));
    }

    try {
      const localConfig = await this.http.get<AppConfig>('/assets/app.config.local.json').toPromise();
      Object.assign(<any> this.config, localConfig);
      console.log(`Loaded local configuration.`);
    } catch (ex) {
      console.log(`No local configuration found.`);
    }

    this.loaded = true;
  }

  getRedirectUri() {
    if (!this.loaded || !this.config) throw new Error('Config not loaded');
    return this.config.redirectUri || window.location.origin + '/';
  }

  getLoginUrl() {
    if (!this.loaded || !this.config) throw new Error('Config not loaded');
    return this.config.loginUrl || window.location.origin + '/';
  }

  getAuthCodeFlowConfig(): AuthConfig {
    if (!this.loaded || !this.config) throw new Error('Config not loaded');

    return {
      // Url of the Identity Provider
      issuer: this.config.idpIssuer,

      // URL of the SPA to redirect the user to after login
      redirectUri: this.getRedirectUri(),
      loginUrl: this.getLoginUrl(),

      // The SPA's id. The SPA is registerd with this id at the auth-server
      clientId: this.config.idpClientId,

      dummyClientSecret: this.config.idpClientSecret,

      // Just needed if your auth server demands a secret. In general, this
      // is a sign that the auth server is not configured with SPAs in mind
      // and it might not enforce further best practices vital for security
      // such applications.
      // dummyClientSecret: 'secret',

      responseType: 'code',

      // set the scope for the permissions the client should request
      // The first four are defined by OIDC.
      // Important: Request offline_access to get a refresh token
      // The api scope is a usecase specific one
      scope: this.config.idpScope,

      showDebugInformation: true
    };
  }
}
