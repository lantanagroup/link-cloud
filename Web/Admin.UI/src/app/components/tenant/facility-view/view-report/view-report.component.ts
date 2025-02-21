import {Component, Input} from '@angular/core';
import {MatCard, MatCardModule} from "@angular/material/card";
import {ValidationResultsComponent} from "../validation-results/validation-results.component";
import {CommonModule} from "@angular/common";
import {MatChipsModule} from "@angular/material/chips";
import {ActivatedRoute, RouterLink} from "@angular/router";
import {F} from "@angular/cdk/keycodes";

@Component({
  selector: 'app-view-report',
  imports: [
    MatCardModule,
    MatChipsModule,
    ValidationResultsComponent,
    CommonModule,
    RouterLink
  ],
  templateUrl: './view-report.component.html',
  styleUrl: './view-report.component.css'
})
export class ViewReportComponent {
  @Input() facilityId!: string;
  @Input() reportId!: string;

  reportInfo = {
    reportId: 'R001',
    reportingPeriodStart: '2023-01-01',
    reportingPeriodEnd: '2023-01-31',
    measures: [{
      shortName: 'HYPO',
      longName: 'Hypoglycemia',
    }, {
      shortName: 'ACH',
      longName: 'Acute Care Hospital'
    }]
  }

  constructor(private route: ActivatedRoute) {
    this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.reportId = params['reportId'];

      // TODO: Get information about the report from the API
    });
  }

  protected readonly F = F;
}
