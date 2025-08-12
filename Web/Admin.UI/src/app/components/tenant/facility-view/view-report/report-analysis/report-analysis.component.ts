import { CommonModule } from '@angular/common';
import { Component, input, OnInit } from '@angular/core';
import { IDataAcquisitionLogStatistics } from 'src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';
import { BarChartComponent } from "../../../../core/bar-chart/bar-chart.component";
import { DonutChartComponent } from "../../../../core/donut-chart/donut-chart.component";

@Component({
  selector: 'app-report-analysis',
  imports: [
    CommonModule,
    BarChartComponent,
    DonutChartComponent
],
  templateUrl: './report-analysis.component.html',
  styleUrl: './report-analysis.component.scss'
})
export class ReportAnalysisComponent implements OnInit {

  readonly data = input<IDataAcquisitionLogStatistics>();  

  constructor() {}

  ngOnInit(): void {
    if(!this.data()) {
      throw new Error('Data Acquisition Log Statistics data is required to render analysis.');
    }
  }

  get queryTypeCounts(): Record<string, number> {
    return this.data()?.queryTypeCounts || {};
  }

  get queryPhaseCounts(): Record<string, number> {
    return this.data()?.queryPhaseCounts || {};
  }

  get requestStatusCounts(): Record<string, number> {
    return this.data()?.requestStatusCounts || {};
  }

  get resourceTypeCounts(): Record<string, number> {
    return this.data()?.resourceTypeCounts || {};
  }

  get resourceTypeCompletionTimeMilliseconds(): Record<string, number> {
    return this.data()?.resourceTypeCompletionTimeMilliseconds || {};
  }

  get totalLogs(): number {
    return this.data()?.totalLogs || 0;
  }

  get totalPatients(): number {
    return this.data()?.totalPatients || 0;
  }

  get totalResourcesAcquired(): number {
    return this.data()?.totalResourcesAcquired || 0;
  }

  get totalRetryAttempts(): number {
    return this.data()?.totalRetryAttempts || 0;
  }

  get totalCompletionTimeMilliseconds(): number {
    return this.data()?.totalCompletionTimeMilliseconds || 0;
  }

  get averageCompletionTimeMilliseconds(): number {
    return this.data()?.averageCompletionTimeMilliseconds || 0;
  }


  get timeStatistics(): Record<string, number> {
    return {
      [`fastest (${this.data()?.fastestCompletionTimeMilliseconds.resourceType})`]: this.data()?.fastestCompletionTimeMilliseconds.completionTimeMilliseconds || 0,
      average: this.averageCompletionTimeMilliseconds,      
      [`slowest (${this.data()?.slowestCompletionTimeMilliseconds.resourceType})`]: this.data()?.slowestCompletionTimeMilliseconds.completionTimeMilliseconds || 0,
      total: this.totalCompletionTimeMilliseconds
    };
  }


}
