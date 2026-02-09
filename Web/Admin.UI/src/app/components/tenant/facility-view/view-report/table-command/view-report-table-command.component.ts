import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faEllipsisV, faEye } from '@fortawesome/free-solid-svg-icons';
import { MatButtonModule } from '@angular/material/button';
import { ClickOutsideDirective } from 'src/app/directives/click-outside.directive';
import { IReportEntry } from '../../../../../interfaces/report/report-entry.interface';

@Component({
  selector: 'app-view-report-table-command',
  standalone: true,
  imports: [FontAwesomeModule, ClickOutsideDirective, MatButtonModule],
  templateUrl: './view-report-table-command.component.html',
  styleUrl: './view-report-table-command.component.scss'
})
export class ViewReportTableCommandComponent implements OnInit {  
  @Input() measureReport: IReportEntry | undefined;
  @Output() viewDetails = new EventEmitter<IReportEntry>();

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
