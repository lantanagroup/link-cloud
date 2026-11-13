import {Component, OnInit} from '@angular/core';
import {MonitorService} from '../monitor.service';
import {MatTableModule} from '@angular/material/table';
import {ILinkServiceHealthSummary} from './link-service-health-summary.interface';
import {MatToolbarModule} from '@angular/material/toolbar';
import {CommonModule} from '@angular/common';
import {MatIconModule} from '@angular/material/icon';
import {IServiceInfoModel, ServiceInfoService} from "../service-info.service";
import {finalize} from 'rxjs';
import {MatButtonModule} from '@angular/material/button';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';

@Component({
  selector: 'app-link-health-check',
  imports: [
    CommonModule,
    MatToolbarModule,
    MatTableModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './link-health-check.component.html',
  styleUrl: './link-health-check.component.scss'
})
export class LinkHealthCheckComponent implements OnInit {
  healthSummary: (ILinkServiceHealthSummary & { serviceKey: string })[] = [];
  serviceInfos: IServiceInfoModel[] = [];
  serviceInfosDisplayColumns: string[] = ['serviceName', 'version', 'productVersion', 'commit', 'build'];

  initializeSummaries = true;
  initializeServiceInfos = true;
  expandedServices = new Set<string>();

  constructor(private monitorService: MonitorService, private serviceInfoService: ServiceInfoService) { }

  ngOnInit(): void {
    this.initHealthSummary();
    this.getHealthSummary();
    this.getServiceInfos();
  }

  // Service key (the Admin BFF's /monitor/health/{service} route value) and the display name the
  // Admin BFF reports for it. Rows are listed alphabetically by display name, so a new service only
  // needs an entry here, in any position.
  static readonly services: ReadonlyArray<[serviceKey: string, displayName: string]> = [
    ['account', 'Account'],
    ['adminbff', 'Admin BFF'],
    ['audit', 'Audit'],
    ['census', 'Census'],
    ['dataacquisition', 'Data Acquisition'],
    ['measureeval', 'Measure Evaluation'],
    ['nhsnappbff', 'NHSN App BFF'],
    ['normalization', 'Normalization'],
    ['querydispatch', 'Query Dispatch'],
    ['report', 'Report'],
    ['submission', 'Submission'],
    ['tenant', 'Tenant'],
    ['terminology', 'Terminology'],
    ['validation', 'Validation']
  ];

  initHealthSummary(): void {
    this.healthSummary = [...LinkHealthCheckComponent.services]
      .sort(([, a], [, b]) => a.localeCompare(b, undefined, {sensitivity: 'base'}))
      .map(([serviceKey, displayName]) => this.createHealthSummary(serviceKey, displayName));
  }

  createHealthSummary(serviceName: string, displayName: string = serviceName): ILinkServiceHealthSummary & { serviceKey: string } {
    return {
      serviceKey: serviceName,
      service: displayName,
      status: 'Loading...',
      totalDuration: '',
      entries: {}
    }
  }

  getHealthSummary(): void {
    let pendingRequests = this.healthSummary.length;
    this.initializeSummaries = pendingRequests > 0;

    this.healthSummary.forEach(summary => {
      this.monitorService.getServiceHealthCheck(summary.serviceKey).pipe(
        finalize(() => {
          pendingRequests--;
          this.initializeSummaries = pendingRequests > 0;
        })
      ).subscribe({
        next: response => {
          Object.assign(summary, response);
        },
        error: error => {
          summary.status = 'Unknown';
          summary.totalDuration = '';
          summary.entries = {};
          console.error(`Error fetching health for ${summary.service}:`, error);
        }
      });
    });
  }

  refreshHealthSummary(): void {
    this.initHealthSummary();
    this.getHealthSummary();
  }

  getServiceInfos(): void {
    this.initializeServiceInfos = true;
    this.serviceInfoService.getServiceInfos().pipe(
      finalize(() => this.initializeServiceInfos = false)
    ).subscribe({
      next: (response: IServiceInfoModel[]) => {
        this.serviceInfos = response.map(s => ({
          ...s,
          serviceName: s.serviceName ?? 'Unknown'
        })).sort((a, b) => {
          if (a.serviceName === 'Unknown' && b.serviceName !== 'Unknown') return 1;
          if (a.serviceName !== 'Unknown' && b.serviceName === 'Unknown') return -1;
          return a.serviceName.localeCompare(b.serviceName);
        });
      },
      error: (error) => {
        console.error('Error fetching service infos:', error);
      }
    })
  }

  formatDuration(duration: string): string {
    const match = /^(-)?(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?$/.exec(duration);
    if (!match) return duration;

    const seconds = Number(match[2] || 0) * 86400 + Number(match[3]) * 3600
      + Number(match[4]) * 60 + Number(match[5]) + Number(`0.${match[6] || '0'}`);
    const sign = match[1] || '';
    if (seconds === 0) return 'N/A';
    if (seconds < 0.001) return '<1 ms';

    const units: [number, string][] = [
      [86400, 'd'], [3600, 'h'], [60, 'min'], [1, 's'], [0.001, 'ms']
    ];
    const [scale, unit] = units.find(([threshold]) => seconds >= threshold) || units[units.length - 1];
    return `${sign}${Number((seconds / scale).toFixed(2))} ${unit}`;
  }

  toggleService(service: string): void {
    if (this.expandedServices.has(service)) {
      this.expandedServices.delete(service);
    } else {
      this.expandedServices.add(service);
    }
  }

}
