import { Component, ViewChild } from '@angular/core';
import { VdButtonComponent } from "../../core/vd-button/vd-button.component";
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { dummyCatSummary } from 'src/assets/dummy-data/sub-pre-qual-report-data';
import { Subscription } from 'rxjs';
import { IValidationIssue, IValidationIssueCategorySummary, IValidationIssuesSummary } from '../../tenant/facility-view/report-view.interface';
import { ActivatedRoute } from '@angular/router';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IApiResponse } from 'src/app/interfaces/api-response.interface';

@Component({
  selector: 'app-sub-pre-qual-report-summary',
  imports: [VdButtonComponent, VdIconComponent, BaseChartDirective],
  templateUrl: './sub-pre-qual-report-summary.component.html',
  styleUrls: ['./sub-pre-qual-report-summary.component.scss']
})
export class SubPreQualReportSummaryComponent {
  private subscription: Subscription | undefined;

  facilityId: string = '';
  submissionId: string = ''
  reportIssues: IValidationIssue[] = [];
  reportIssuesSummary: IValidationIssueCategorySummary[] = [];

  issuesResponse: IValidationIssue[] | undefined;
  issuesSummaryResponse: IValidationIssueCategorySummary[] | undefined;

  @ViewChild(BaseChartDirective) chart: BaseChartDirective<'bar'> | undefined;

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

  public barChartData: ChartData<'bar', IValidationIssueCategorySummary[]> = {
    datasets: [{
      barThickness: 64,
      data: []
    }],
  };

  constructor(
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit(): void {
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
    })

    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.issuesResponse = response;
        this.reportIssues = this.issuesResponse;

        this.facilityViewService.getReportIssuesSummary(this.reportIssues).subscribe({
          next: (response) => {
            this.issuesSummaryResponse = response;
            this.reportIssuesSummary = this.issuesSummaryResponse;
            this.barChartData.datasets[0].data = this.reportIssuesSummary;
            console.log('this.barChartData ->', this.barChartData);
          }
        })
      }
    })
  }
}
