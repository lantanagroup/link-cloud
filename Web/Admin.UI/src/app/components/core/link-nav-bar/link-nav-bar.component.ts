import { IsActiveMatchOptions, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { NgFor, NgIf } from '@angular/common';

import { Component } from '@angular/core';
import { VdIconComponent } from "../vd-icon/vd-icon.component";
import { AppConfigService } from "../../../services/app-config.service";

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
    VdIconComponent,
    NgFor,
    NgIf
  ],
  templateUrl: './link-nav-bar.component.html',
  styleUrls: ['./link-nav-bar.component.scss'],
  standalone: true,
})
export class LinkNavBarComponent {
  constructor(private router: Router, private appConfig: AppConfigService) { }

  subnavItems: SubnavItem[] = [
    { label: 'Home', path: '/' },
    { label: 'Tenants', path: '/tenant' },
    { label: 'Reports', path: '/reports' },
    {
      label: 'Configuration',
      children: [
        { label: 'Implementation Guides', path: '/validation-config' },
        { label: 'Measure Definitions', path: '/measure-def' },
        { label: 'Normalization Operations', path: '/tenant/operations' },
        { label: 'Query Plans', path: '/query-plans' },
        { label: 'Validation Categories', path: '/validation-config/validation-categories' },
        { label: 'Vendors', path: '/vendor' },
      ]
    },
    {
      label: 'Logs',
      children: [
        { label: 'Acquisition Log', path: '/tenant/acquisition-log' },
        { label: 'Audit Event Log', path: '/audit' },
        { label: 'Grafana', path: this.appConfig?.config?.grafanaUrl || '/' },
        { label: 'Kafka', path: '/kafka' },
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
}
