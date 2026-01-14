import { Component, OnInit } from '@angular/core';
import { MonitorService } from '../monitor.service';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { ILinkServiceHealthSummary } from './link-service-health-summary.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

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
  dataSource!: MatTableDataSource<ILinkServiceHealthSummary>;
  healthSummary: ILinkServiceHealthSummary[] = [];
  displayedColumns: string[] = ['ServiceName', 'ServiceHealthStatus', 'KafkaConnection', 'DatabaseConnection', 'CacheConnection'];

  initializeSummaries = true;

  constructor(private monitorService: MonitorService) { }

  ngOnInit(): void {
    this.dataSource = new MatTableDataSource<ILinkServiceHealthSummary>();
    this.getLinkHealthSummary();    
  }

  getLinkHealthSummary(): void {
    this.monitorService.getLinkHealthCheck().subscribe({
      next: (response: ILinkServiceHealthSummary[]) => {
        this.healthSummary = response;
        this.dataSource.data = this.healthSummary;
        this.initializeSummaries = false;
      },
      error: (error) => {
        console.error('Error fetching link health summary:', error);
      } 
    })
  }

  onRefresh(): void {
    this.getLinkHealthSummary();
  }

}
