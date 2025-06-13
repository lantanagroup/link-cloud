import { Component, ViewChild } from '@angular/core';
import { VdButtonComponent } from "../../core/vd-button/vd-button.component";
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData } from 'chart.js';
import { dummyCatSummary } from 'src/assets/dummy-data/sub-pre-qual-report-data';

@Component({
  selector: 'app-sub-pre-qual-report-summary',
  imports: [VdButtonComponent, VdIconComponent, BaseChartDirective],
  templateUrl: './sub-pre-qual-report-summary.component.html',
  styleUrls: ['./sub-pre-qual-report-summary.component.scss'],
  standalone: true
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
  };

  public barChartType = 'bar' as const;

  public barChartData: ChartData<'bar', { x: string, y: number }[]> = {
    datasets: [{
      barThickness: 64,
      data: dummyCatSummary
    }]
  };
}
