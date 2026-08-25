import { HttpClient } from "@angular/common/http";
import { AuthConfig } from 'angular-oauth2-oidc';
import { Injectable } from "@angular/core";

export interface AppConfig {
  baseApiUrl: string;
  authRequired: boolean;
  allowAlphaNumericFacilityId: boolean;
  /**
   * DMRP feature flag — a system-wide switch, not a Tenant setting. The services read it from the
   * DMRP:Enabled key; this app cannot read App Configuration, so its container maps LINK_DMRP_ENABLED
   * into the runtime config instead. Both carry the same decision and must agree.
   *
   * When it is on, a facility's scheduled reports are derived from its DMRP reporting plans and the
   * API refuses a facility that supplies its own, so the form hides the report pickers and submits
   * an empty schedule.
   *
   * This must never be on here while it is off in the services: the form would then send an empty
   * schedule that Tenant accepts, quietly creating a facility that reports nothing. The other way
   * round fails loudly instead.
   *
   * This flag is temporary and expected to end up permanently on. To retire it, grep for
   * "DMRP feature flag" and:
   *   1. delete this property, the dmrpEnabled getter on FacilityConfigFormComponent, the
   *      LINK_DMRP_ENABLED block in server/main.js, and the key in assets/app.config.json;
   *   2. in facility-config-form.component.html keep the @if body and delete the @else that holds
   *      the report pickers;
   *   3. in submitConfiguration keep the empty arrays and delete the conditionals;
   *   4. remove LINK_DMRP_ENABLED from docker-compose.yml.
   */
  dmrpEnabled: boolean;
  oauth2?: {
    enabled: boolean;
    issuer: string;
    clientId: string;
    scope: string;
    responseType: string;
    requireHttps: boolean;
    disablePKCE: boolean;
    skipIssuerCheck: boolean;
  },
  kafkaUrl?: string;
  grafanaUrl?: string;
  // Mirrors the backend's DMRP:Enabled flag; gates the DMRP screens (measure mappings).
  dmrpEnabled?: boolean;
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
