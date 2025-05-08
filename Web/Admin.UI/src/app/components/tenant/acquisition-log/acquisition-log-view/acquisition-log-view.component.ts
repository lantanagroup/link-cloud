import { animate, style, transition, trigger } from '@angular/animations';
import { Location } from '@angular/common';
import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { AcquisitionLogSummary } from '../models/acquisition-log-summary';
import { AcquisitionLogService } from '../acquisition-log.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faRotate, faArrowLeft } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-acquisition-log-view',
  imports: [
    CommonModule,
    FontAwesomeModule
  ],
  templateUrl: './acquisition-log-view.component.html',
  styleUrl: './acquisition-log-view.component.scss',
  animations: [
    trigger('fadeInSlideUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ])
  ]
})
export class AcquisitionLogViewComponent {
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;

  acquisitionLogs: AcquisitionLogSummary[] = [];
  animatedRows = new Set<string>();

  constructor(
    private location: Location,
    private acquisitionLogService: AcquisitionLogService) {   
    this.loadLogs();    
  }

  loadLogs(): void {

    // this.acquisitionLogService.getAcquisitionLogs().subscribe((logs: AcquisitionLogSummary[]) => {
    //   this.acquisitionLogs = logs
    // });

    //create test data for 3 acquisition logs
    this.acquisitionLogs = [
      {
        id: '1',
        priority: 'Normal',
        patientId: '12345',
        facilityId: 'TestFacility',
        resourceTypes: ['Patient'],
        resourceId: '12345',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Read',
        scheduledDate: new Date(),
        status: 'Completed'
      },
      {
        id: '2',
        priority: 'Normal',
        patientId: '12345',
        facilityId: 'TestFacility',
        resourceTypes: ['Encounter'],
        resourceId: '',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Search',
        scheduledDate: new Date(),
        status: 'Pending'
      }
    ];
  }

  navBack(): void {
    this.location.back();
  }

}
