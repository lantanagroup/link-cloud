import { Component, OnInit } from '@angular/core';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../../services/gateway/notification.service';
import { NotificationModel } from '../../../models/notification/notification-model.model';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { ITableHeaderModel } from '../../../interfaces/table-header-model.interface';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatExpansionModule } from '@angular/material/expansion';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { OverlayContainer } from '@angular/cdk/overlay';
import { CreateNotificationDialogComponent } from './create-notification-dialog/create-notification-dialog.component';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-notification-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatPaginatorModule,
    MatExpansionModule,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatToolbarModule,
    MatListModule,
    MatChipsModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  templateUrl: './notification-dashboard.component.html',
  styleUrls: ['./notification-dashboard.component.scss'],
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: '*' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
})
export class NotificationDashboardComponent implements OnInit {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  notifications: NotificationModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  displayedColumns: ITableHeaderModel[] = [{ key: 'id', display: 'Id' }, { key: 'facilityId', display: 'Facility' }, { key: 'notificationType', display: 'Type' }, { key: 'createdOn', display: 'Created On' }, { key: 'sentOn', display: 'Sent On' }];
  columnsToDisplayWithExpand = [...this.displayedColumns.map(x => x.key), 'expand'];
  expandedRecord: NotificationModel | null | undefined;
  dataSource = new MatTableDataSource<NotificationModel>(this.notifications);

  //search parameters
  searchText: string = '';
  filterFacilityBy: string = '';
  filterNotificationTypeBy: string = '';
  createdOnStart: Date | null = null;
  createdOnEnd: Date | null = null;
  sentOnStart: Date | null = null;
  sentOnEnd: Date | null = null;
  sortBy: string = '';

  //filters
  facilityFilter = ["All", "FACILITY_ORG_10001", "FACILITY_ORG_12345"]; //temp until tenant functionality is added
  notificationTypeFilter = ["All"];

  constructor(private notificationSerice: NotificationService, private dialog: MatDialog, private overlay: OverlayContainer, private snackBar: MatSnackBar) { }


  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getNotifications();
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getNotifications();
  }

  getNotifications() {
    this.notificationSerice.list(this.searchText, this.filterFacilityBy, this.filterNotificationTypeBy, this.createdOnStart, this.createdOnEnd, this.sentOnStart,
      this.sentOnEnd, this.sortBy, this.paginationMetadata.pageSize, this.paginationMetadata.pageNumber).subscribe(data => {
        this.notifications = data.records;
        this.paginationMetadata = data.metadata;
      });
  }

  onFilterSelection(filter: MatSelectChange) {

    switch (filter.source.id) {
      case ("facilityFilterSelector"):
        {
          if (filter.value === 'All') {
            this.filterFacilityBy = '';
          }
          else {
            this.filterFacilityBy = filter.value;
          }
        }
        break;
      case ("filterNotificationTypeBy"):
        {
          if (filter.value === 'All') {
            this.filterNotificationTypeBy = '';
          }
          else {
            this.filterNotificationTypeBy = filter.value;
          }
        }
        break;
    }

    this.getNotifications();
  }

  clearFilters() {
    this.searchText = '';
    this.filterFacilityBy = '';
    this.filterNotificationTypeBy = '';
    this.sortBy = '';

    this.getNotifications();
  }

  showCreateNotificationDialog(): void {
    this.dialog.open(CreateNotificationDialogComponent,
      {
        width: '50%'
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {
          this.getNotifications();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

}
