import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { LinkAdminSubnavBarComponent } from '../../core/link-admin-subnav-bar/link-admin-subnav-bar.component';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { Issue } from 'src/app/interfaces/sub-pre-qual-report-models.interface';
import { dummyIssues } from 'src/assets/dummy-data/sub-pre-qual-report-data';

@Component({
  selector: 'app-validation-categories',
  imports: [
    CommonModule,
    LinkAdminSubnavBarComponent,
    MatTableModule,
    MatSortModule
  ],
  templateUrl: './validation-categories.component.html',
  styleUrls: ['./validation-categories.component.scss']
})
export class ValidationCategoriesComponent {
  @ViewChild('sort', { static: true }) sort: MatSort = new MatSort;

  dataSource: MatTableDataSource<Issue> = new MatTableDataSource<Issue>;
  validationCategoryColumns: string[] = ['category', 'severity', 'acceptability', 'guidance', 'rules'];

  constructor(
    private cd: ChangeDetectorRef,
    private el: ElementRef
  ) { }

  ngOnInit() {
    this.dataSource = new MatTableDataSource(dummyIssues);
    this.dataSource.sort = this.sort;
  }
}
