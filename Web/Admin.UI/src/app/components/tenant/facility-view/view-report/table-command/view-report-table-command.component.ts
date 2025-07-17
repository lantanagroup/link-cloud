import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faEllipsisV, faEye } from '@fortawesome/free-solid-svg-icons';
import { ClickOutsideDirective } from 'src/app/directives/click-outside.directive';
import { IMeasureReportSummary } from '../../report-view.interface';

@Component({
  selector: 'app-view-report-table-command',
  standalone: true,
  imports: [CommonModule, FontAwesomeModule, ClickOutsideDirective],
  templateUrl: './view-report-table-command.component.html',
  styleUrl: './view-report-table-command.component.scss'
})
export class ViewReportTableCommandComponent implements OnInit {  
  @Input() measureReport: IMeasureReportSummary | undefined;
  @Output() viewDetails = new EventEmitter<IMeasureReportSummary>();

  faEllipsisV = faEllipsisV;
  faEye = faEye;
  isOpen = false;

  constructor() { } 

  ngOnInit(): void {
    if(!this.measureReport) {
      throw new Error('Measure Report is required');
    }
  }

  toggleMenu() {
    this.isOpen = !this.isOpen;
  }

  onViewDetails() {
    if (this.measureReport) {
      this.isOpen = false;
      this.viewDetails.emit(this.measureReport);
    } else {
      throw new Error('Measure Report is required');
    }
  }

}