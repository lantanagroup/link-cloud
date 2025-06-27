import { ChartConfiguration, ChartData } from 'chart.js';
import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { IValidationIssue, IValidationIssueCategorySummary, IValidationIssuesSummary } from '../../tenant/facility-view/report-view.interface';

import { ActivatedRoute } from '@angular/router';
import { BaseChartDirective } from 'ng2-charts';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IApiResponse } from 'src/app/interfaces/api-response.interface';
import { Subscription } from 'rxjs';
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
export class SubPreQualReportSummaryComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;

  facilityId: string = '';
  submissionId: string = ''
  reportIssues: IValidationIssue[] = [];
  reportIssuesSummary: IValidationIssueCategorySummary[] = [];

  issuesResponse: IValidationIssue[] | undefined;
  issuesSummaryResponse: IValidationIssueCategorySummary[] | undefined;

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
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit(): void {
    // Subscribe to route parameters to get facilityId and submissionId
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];

      // Load data when params change
      this.loadReportData();
    });
  }

  /**
   * Loads report data from the API
   * First gets all issues, then gets their summary
   * Updates the chart with the summary data
   */
  private loadReportData(): void {
    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.issuesResponse = response;
        this.reportIssues = this.issuesResponse;

        if (this.reportIssues.length > 0) {
          this.facilityViewService.getReportIssuesSummary(this.reportIssues).subscribe({
            next: (response) => {
              this.issuesSummaryResponse = response;
              this.reportIssuesSummary = this.issuesSummaryResponse;

              // Update chart data with the summary
              this.barChartData = {
                datasets: [{
                  barThickness: 64,
                  data: this.reportIssuesSummary
                }]
              };

              // Force chart update to reflect new data
              if (this.chart) {
                this.chart.update();
              }
            },
            error: (error) => {
              console.error('Error getting report issues summary:', error);
            }
          });
        } else {
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
      },
      error: (error) => {
        console.error('Error getting report issues:', error);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
