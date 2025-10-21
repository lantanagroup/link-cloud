import { ChartConfiguration, ChartData } from 'chart.js';
import { Component, ViewChild, Input, OnChanges, SimpleChanges } from '@angular/core';
import { IValidationIssue, IValidationIssueCategorySummary, IValidationIssuesSummary } from '../../tenant/facility-view/report-view.interface';

import { BaseChartDirective } from 'ng2-charts';
import { VdButtonComponent } from "../../core/vd-button/vd-button.component";
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";

/**
 * Component that displays a summary of report issues in a bar chart
 * Shows the count of issues by category
 * Uses Chart.js for visualization
 */
@Component({
  selector: 'app-sub-pre-qual-report-summary',
  imports: [VdButtonComponent, VdIconComponent, BaseChartDirective],
  templateUrl: './sub-pre-qual-report-summary.component.html',
  styleUrls: ['./sub-pre-qual-report-summary.component.scss'],
  standalone: true
})
export class SubPreQualReportSummaryComponent implements OnChanges {
  @Input() reportIssues: IValidationIssue[] | undefined;
  @Input() reportIssuesSummary: IValidationIssueCategorySummary[] | undefined;

  @ViewChild(BaseChartDirective) chart: BaseChartDirective<'bar'> | undefined;

  /**
   * Chart configuration options
   * Customizes the appearance and behavior of the bar chart
   */
  public barChartOptions: ChartConfiguration<'bar'>['options'] = {
    // We use these empty structures as placeholders for dynamic theming.
    scales: {
      x: {
        border: {
          display: false,
        },
        grid: {
          display: false,
        },
        ticks: {
          font: {
            family: "'Tahoma', 'Arial', sans-serif",
            size: 10
          },
          color: "#000000",
          minRotation: 30,
          maxRotation: 45
        }
      },
      y: {
        beginAtZero: true,
        border: {
          display: false,
        },
        grid: {
          display: false,
        },
        ticks: {
          font: {
            family: "'Tahoma', 'Arial', sans-serif",
            size: 10
          },
          color: "#000000"
        }
      }
    },
    animation: false,
    animations: {
      colors: false,
      x: false,
    },
    backgroundColor: "#712177",
    transitions: {
      active: {
        animation: {
          duration: 0,
        }
      }
    },
    plugins: {
      legend: {
        display: false,
      },
      tooltip: {
        enabled: false,
      }
    },
    parsing: {
      xAxisKey: 'value',
      yAxisKey: 'count'
    }
  };

  public barChartType = 'bar' as const;

  /**
   * Chart data structure
   * Contains the dataset for the bar chart
   * Each bar represents a category with its issue count
   */
  public barChartData: ChartData<'bar', IValidationIssueCategorySummary[]> = {
    datasets: [{
      barThickness: 64,
      data: [],
    }],
  };

  constructor(
    
  ) { }

  ngOnChanges(changes: SimpleChanges): void {
    // Only reload if reportIssues or reportIssuesSummary inputs have changed
    if (changes['reportIssues'] || changes['reportIssuesSummary']) {
      this.loadReportData();
    }
  }

  /**
   * Updates the chart with the summary data
   */
  private loadReportData(): void {
    if (this.reportIssuesSummary && this.reportIssuesSummary.length > 0) {
      // Update chart data with the summary
      this.barChartData = {
        datasets: [{
          barThickness: 64,
          data: this.reportIssuesSummary ?? []
        }]
      };

      // Force chart update to reflect new data
      if (this.chart) {
        this.chart.update();
      }
    }
    else {
      // No issues found, clear the chart
      this.barChartData = {
        datasets: [{
          barThickness: 64,
          data: []
        }]
      };
      if (this.chart) {
        this.chart.update();
      }
    }
  }
}
