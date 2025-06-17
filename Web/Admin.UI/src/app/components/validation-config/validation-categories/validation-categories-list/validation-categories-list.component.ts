import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { LinkAdminSubnavBarComponent } from '../../../core/link-admin-subnav-bar/link-admin-subnav-bar.component';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { Issue } from 'src/app/interfaces/sub-pre-qual-report-models.interface';
import { ValidationService } from 'src/app/services/gateway/validation/validation.service';
import { IValidationIssueCategory } from '../../../tenant/facility-view/report-view.interface';
import { Subscription } from 'rxjs';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-validation-categories-list',
  imports: [
    CommonModule,
    LinkAdminSubnavBarComponent,
    MatTableModule,
    MatSortModule,
    RouterModule
  ],
  templateUrl: './validation-categories-list.component.html',
  styleUrls: ['./validation-categories-list.component.scss']
})
export class ValidationCategoriesComponent {
  private subscription: Subscription | undefined;

  @ViewChild('sort', { static: true }) sort: MatSort = new MatSort;

  dataSource: MatTableDataSource<IValidationIssueCategory> = new MatTableDataSource<IValidationIssueCategory>;
  // validationCategoryColumns: string[] = ['category', 'severity', 'acceptability', 'guidance', 'rules'];
  columns = [
    { header: 'Category', key: 'title' },
    { header: 'Severity', key: 'severity' },
    { header: 'Acceptability', key: 'acceptable' },
    { header: 'Guidance', key: 'guidance' },
    { header: 'Rules', key: 'rules' }
  ];
  columnKeys = this.columns.map(col => col.key);

  validationCategories: IValidationIssueCategory[] | undefined

  constructor(
    private validationService: ValidationService
  ) { }

  ngOnInit() {
    this.dataSource = new MatTableDataSource();
    this.dataSource.sort = this.sort;

    this.validationService.getValidationCategories().subscribe({
      next: (response) => {
        this.validationCategories = response;
        console.log('validationCategories ->', this.validationCategories)
        this.dataSource.data = this.validationCategories;
      }
    })
  }
}
