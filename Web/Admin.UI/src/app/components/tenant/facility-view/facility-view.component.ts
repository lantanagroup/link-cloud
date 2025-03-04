import {Component, Input} from '@angular/core';
import {MatTableModule} from "@angular/material/table";
import {ActivatedRoute, RouterLink} from "@angular/router";
import {MatCardModule} from "@angular/material/card";

@Component({
  selector: 'app-facility-view',
  imports: [
    MatTableModule,
    RouterLink,
    MatCardModule
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.css'
})
export class FacilityViewComponent {
  @Input() facilityId!: string;
  nhsnOrgId: string = 'TODO';

  displayedColumns: string[] = ['reportId', 'reportingPeriod', 'measures'];
  reports = [
    { reportId: 'R001', reportingPeriod: '2023 Q1', measures: 'Measure A, Measure B' },
    { reportId: 'R002', reportingPeriod: '2023 Q2', measures: 'Measure C, Measure D' }
  ];

  constructor(private route: ActivatedRoute) {
    this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];

      // TODO: Get information about the tenant/facility (i.e. nhsnOrgId and list of reports) from the API
    });
  }
}
