import { HttpClient } from "@angular/common/http";
import { AuthConfig } from 'angular-oauth2-oidc';
import { Injectable } from "@angular/core";

export interface AppConfig {
  baseApiUrl: string;
  authRequired: boolean;
  allowAlphaNumericFacilityId: boolean;
  oauth2?: {
    enabled: boolean;
    issuer: string;
    clientId: string;
    scope: string;
    responseType: string;
    requireHttps: boolean;
    disablePKCE: boolean;
    skipIssuerCheck: boolean;
  }
}

@Injectable({
  providedIn: 'root'
})
export class AppConfigService {
  public config?: AppConfig;

  loaded = false;

  constructor(private http: HttpClient) { }

  async loadConfig(): Promise<AppConfig|undefined> {
    let config;

    try {
      config = await this.http.get<AppConfig>('/assets/app.config.json').toPromise();
    } catch (ex: any) {
      throw new Error('Failed to acquire app configuration: ' + (ex.message || ex));
    }

    try {
      const localConfig = await this.http.get<AppConfig>('/assets/app.config.local.json').toPromise();
      config = AppConfigService.deepMerge(config, localConfig);
      console.log(`Loaded local configuration.`);
    } catch (ex) {
      console.log(`No local configuration found.`);
    }

    this.config = config;
    this.loaded = true;
    return config;
  }

  static deepMerge(target: any, source: any): any {
    const output = { ...target };
    for (const key in source) {
      if (
        source[key] instanceof Object &&
        key in target &&
        target[key] instanceof Object
      ) {
        output[key] = AppConfigService.deepMerge(target[key], source[key]);
      } else {
        output[key] = source[key];
      }
    }
    return output;
  }
}
