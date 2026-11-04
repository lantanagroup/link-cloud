import {Component, EventEmitter, Input, Output} from '@angular/core';
import {MatTableModule} from '@angular/material/table';
import {MatPaginatorModule, PageEvent} from '@angular/material/paginator';
import {ICategoryIssue} from '../../tenant/facility-view/report-view.interface';

@Component({
  selector: 'app-sub-pre-qual-report-issues-table',
  imports: [MatTableModule, MatPaginatorModule],
  templateUrl: './sub-pre-qual-report-issues-table.component.html',
  styleUrls: ['./sub-pre-qual-report-issues-table.component.scss'],
  standalone: true
})
export class SubPreQualReportIssuesTableComponent {
  @Input() reportIssues: ICategoryIssue[] = [];
  @Input() totalCount = 0;
  @Input() pageIndex = 0;
  @Input() pageSize = 25;
  @Output() pageChange = new EventEmitter<PageEvent>();
  issueColumns = ['message', 'patientId', 'expression', 'location'];
}
