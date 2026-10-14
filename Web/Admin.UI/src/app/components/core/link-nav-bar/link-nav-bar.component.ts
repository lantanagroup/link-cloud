import {Router, RouterLink, RouterLinkActive} from '@angular/router';


import {Component, OnInit} from '@angular/core';
import {VdIconComponent} from "../vd-icon/vd-icon.component";
import {AppConfigService} from "../../../services/app-config.service";
import {DmrpStatusService} from "../../../services/gateway/dmrp/dmrp-status.service";

export interface SubnavItem {
  label: string;
  path?: string; // For routerLink
  children?: SubnavItem[]; // For dropdowns
}

@Component({
  selector: 'link-nav-bar',
  imports: [
    RouterLink,
    RouterLinkActive,
    VdIconComponent
],
  templateUrl: './link-nav-bar.component.html',
  styleUrls: ['./link-nav-bar.component.scss'],
  standalone: true,
})
export class LinkNavBarComponent implements OnInit {
  constructor(private router: Router,
              private appConfig: AppConfigService,
              private dmrpStatusService: DmrpStatusService) { }

  subnavItems: SubnavItem[] = this.buildSubnavItems(false);

  ngOnInit(): void {
    this.dmrpStatusService.getStatus().subscribe(status => {
      this.subnavItems = this.buildSubnavItems(status.dmrpEnabled);
    });
  }

  private buildSubnavItems(dmrpEnabled: boolean): SubnavItem[] {
    return [
      { label: 'Home', path: '/dashboard' },
      { label: 'Tenants', path: '/tenant' },
      {
        label: 'Reports',
        path: '/reports',
        children: [
          { label: 'Generate Ad-Hoc Report', path: '/reports/generate-report' }
        ]
      },
      {
        label: 'Configuration',
        children: [
          { label: 'HSLOC', path: '/hsloc' },
          { label: 'Implementation Guides', path: '/validation-config' },
          { label: 'Measure Definitions', path: '/measure-def' },
          // DMRP screens only exist while Tenant reports DMRP enabled (DmrpGuard covers direct URLs).
          ...(dmrpEnabled ? [{ label: 'Measure Mappings', path: '/measure-mappings' }] : []),
          { label: 'Normalization Operations', path: '/tenant/operations' },
          { label: 'Query Plans', path: '/query-plans' },
          { label: 'Terminology', path: '/terminology-config' },
          { label: 'Validation Categories', path: '/validation-config/validation-categories-management' },
          { label: 'Vendors', path: '/vendor' },
        ]
      },
      {
        label: 'Logs',
        children: [
          { label: 'Acquisition Log', path: '/tenant/acquisition-log' },
          { label: 'SFTP Acquisition Log', path: '/data-acquisition/sftp-logs' },
          { label: 'Audit Event Log', path: '/audit' },
          { label: 'Grafana', path: this.appConfig?.config?.grafanaUrl || '/' },
          { label: 'Kafka', path: '/kafka' }        
        ]
      },
      {
        label: 'System',
        children: [
          { label: 'App Configuration', path: '/app-configuration' },
          { label: 'Integration Test', path: '/integration-test' },
          { label: 'Health', path: '/monitor/health' },
          { label: 'Users', path: '/account' },
        ]
      },
    ];
  }

  isChildRouteActive(children: SubnavItem[]): boolean {
    return children.some(child =>
      child.path?.startsWith('/') && this.router.isActive(child.path, {
        paths: 'exact',
        queryParams: 'ignored',
        fragment: 'ignored',
        matrixParams: 'ignored'
      })
    );
  }

  isRouteActive(path: string): boolean {
    return this.router.isActive(path, {
      paths: 'exact',
      queryParams: 'ignored',
      fragment: 'ignored',
      matrixParams: 'ignored'
    });
  }

  closeMenu(event: Event): void {
    if (event.currentTarget instanceof HTMLElement) {
      event.currentTarget.blur();
    }
  }

  openMenu(event: Event): void {
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur();
    }
    if (event.currentTarget instanceof HTMLElement) {
      event.currentTarget.focus();
    }
  }
}
