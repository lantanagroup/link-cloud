import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { LinkAdminSubnavBarComponent } from '../../../core/link-admin-subnav-bar/link-admin-subnav-bar.component';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { ValidationService } from 'src/app/services/gateway/validation/validation.service';
import { IValidationIssueCategory } from '../../../tenant/facility-view/report-view.interface';
import { Subscription } from 'rxjs';
import { RouterModule } from '@angular/router';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';
import { VdIconComponent } from 'src/app/components/core/vd-icon/vd-icon.component';

@Component({
  selector: 'app-validation-categories-list',
  imports: [
    CommonModule,
    LinkAdminSubnavBarComponent,
    MatTableModule,
    MatSortModule,
    RouterModule,
    VdButtonComponent,
    VdIconComponent,
  ],
  templateUrl: './validation-categories-list.component.html',
  styleUrls: ['./validation-categories-list.component.scss']
})
export class ValidationCategoriesComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;

  @ViewChild('sort', { static: true }) sort: MatSort = new MatSort;

  dataSource: MatTableDataSource<IValidationIssueCategory> = new MatTableDataSource<IValidationIssueCategory>;
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

    this.subscription = this.validationService.getValidationCategories().subscribe({
      next: (response) => {
        this.validationCategories = response;
        this.dataSource.data = this.validationCategories;
      }
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
