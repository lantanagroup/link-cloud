import {Component, Input, OnChanges, ViewChild} from '@angular/core';
import {IReportCategoryCount} from '../../tenant/facility-view/report-view.interface';
import {MatSort, MatSortModule} from '@angular/material/sort';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {RouterModule} from '@angular/router';
import {DecimalPipe} from '@angular/common';

interface CategoryRow {
  id: string;
  name: string;
  quantity: number;
  guidance: string;
}

@Component({
  selector: 'app-sub-pre-qual-report-categories-table',
  imports: [MatTableModule, MatSortModule, RouterModule, DecimalPipe],
  templateUrl: './sub-pre-qual-report-categories-table.component.html',
  styleUrls: ['./sub-pre-qual-report-categories-table.component.scss'],
  standalone: true
})
export class SubPreQualReportCategoriesTableComponent implements OnChanges {
  @Input() showAcceptable = false;
  @Input() categoryCounts: IReportCategoryCount[] = [];
  @ViewChild('outerSort', {static: true}) sort!: MatSort;

  dataSource = new MatTableDataSource<CategoryRow>();
  categoryColumns = [
    {header: 'Category', key: 'name'},
    {header: 'Number of Issues', key: 'quantity'},
    {header: 'Guidance', key: 'guidance'}
  ];
  categoryColumnKeys = this.categoryColumns.map(column => column.key);

  ngOnChanges(): void {
    this.dataSource.data = this.categoryCounts
      .filter(category => category.acceptable === this.showAcceptable && category.issues > 0)
      .map(category => ({
        id: category.categoryId.toLowerCase() === 'uncategorized' ? 'Uncategorized' : category.categoryId,
        name: category.title,
        quantity: category.issues,
        guidance: category.guidance ?? ''
      }));
    this.dataSource.sort = this.sort;
  }
}
