import { Component, ViewChild } from '@angular/core';
import { VdButtonComponent } from "../../core/vd-button/vd-button.component";
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';

@Component({
  selector: 'app-sub-pre-qual-report-summary',
  imports: [VdButtonComponent, VdIconComponent, BaseChartDirective],
  templateUrl: './sub-pre-qual-report-summary.component.html',
  styleUrls: ['./sub-pre-qual-report-summary.component.scss']
})
export class SubPreQualReportSummaryComponent {
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
      },
      y: {
        beginAtZero: true,
        border: {
          display: false,
        },
        grid: {
          display: false,
        },
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
  };

  public barChartType = 'bar' as const;

  public barChartData: ChartData<'bar', { x: string, y: number }[]> = {
    datasets: [{
      barThickness: 64,
      data: [
        {
          x: "Uncategorized",
          y: 57
        },
        {
          x: "Does not match extensible ValueSet",
          y: 21
        },
        {
          x: "No codes from an extensible binding ValueSet",
          y: 21
        },
        {
          x: "Unknown Code System",
          y: 9
        },
        {
          x: "Minimum requirement not met for profile.",
          y: 6
        },
        {
          x: "Unable to validate measure (Measure not found)",
          y: 3
        }
      ]
    }]
  };
}
