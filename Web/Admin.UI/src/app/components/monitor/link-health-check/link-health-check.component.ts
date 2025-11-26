import { Component, OnInit } from '@angular/core';
import { MonitorService } from '../monitor.service';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { ILinkServiceHealthSummary } from './link-service-health-summary.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import {IServiceInfoModel, ServiceInfoService} from "../service-info.service";

@Component({
  selector: 'app-link-health-check',
  imports: [
    CommonModule,
    MatToolbarModule,
    MatTableModule,
    MatIconModule
  ],
  templateUrl: './link-health-check.component.html',
  styleUrl: './link-health-check.component.scss'
})
export class LinkHealthCheckComponent implements OnInit {
  healthDataSource!: MatTableDataSource<ILinkServiceHealthSummary>;
  serviceInfosDataSource!: MatTableDataSource<IServiceInfoModel>;
  healthSummary: ILinkServiceHealthSummary[] = [];
  serviceInfos: IServiceInfoModel[] = [];
  healthDisplayColumns: string[] = ['ServiceName', 'ServiceHealthStatus', 'KafkaConnection', 'DatabaseConnection', 'CacheConnection'];
  serviceInfosDisplayColumns: string[] = ['serviceName', 'version', 'productVersion', 'commit', 'build'];

  initializeSummaries = true;
  initializeServiceInfos = true;

  constructor(private monitorService: MonitorService, private serviceInfoService: ServiceInfoService) { }

  ngOnInit(): void {
    this.healthDataSource = new MatTableDataSource<ILinkServiceHealthSummary>();
    this.getHealthSummary();
    this.getServiceInfos();
  }

  getHealthSummary(): void {
    this.monitorService.getLinkHealthCheck().subscribe({
      next: (response: ILinkServiceHealthSummary[]) => {
        this.healthSummary = response;
        this.healthDataSource.data = this.healthSummary;
      },
      error: (error) => {
        console.error('Error fetching link health summary:', error);
      },
      complete: () => {
        this.initializeSummaries = false;
      }
    })
  }

  getServiceInfos(): void {
    this.serviceInfoService.getServiceInfos().subscribe({
      next: (response: IServiceInfoModel[]) => {
        this.serviceInfos = response;
        this.serviceInfosDataSource.data = this.serviceInfos;
      },
      error: (error) => {
        console.error('Error fetching service infos:', error);
      },
      complete: () => {
        this.initializeServiceInfos = false;
      }
    })
  }

}
